using Marketplace.Models;
using Marketplace.Utility;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Marketplace.Services
{
    /// <summary>
    /// AiImageService - talks to an OpenAI compatible vision API and turns uploaded images into a draft listing.
    /// Isolation: Every HTTP call builds a fresh chat payload with only a system message and a single user message.
    /// No conversation history is ever reused. Each of the up to three calls per generation (main image,
    /// optional category-only fallback, optional extra details) is a completely new chat, so the model cannot
    /// get confused by previous images or answers. This also keeps concurrent users isolated.
    /// Image slots: 1 main image (required, slot 1) plus up to 3 optional extra images (slots 2 to 4). Total never exceeds 4.
    /// Design intent:
    ///   The main image (slot 1) has absolute precedence. It alone decides Title, Description and CategoryId.
    ///   The 3 optional extra images (slots 2 to 4) are additional angles of the same physical item.
    ///   They are used only to enrich the Description. They never override Title or CategoryId.
    ///   Categories are fetched live from the database via CategoryModel and presented as the exact dropdown options.
    ///   No category name or id is hardcoded. The service always tries to return the best matching CategoryId.
    ///   Every entry point is wrapped so it never throws. On failure it returns null and the controller shows a friendly message.
    /// </summary>
    public class AiImageService(HttpClient httpClient, ApplicationDbContext context) : IAiImageService
    {
        private readonly HttpClient _httpClient = httpClient;
        private readonly ApplicationDbContext _context = context;

        private const int MaxImages = 4;
        private const int MaxImageBytes = 5 * 1024 * 1024;
        private const int MaxTokensMain = 300;
        private const int MaxTokensExtra = 250;
        private const int KeepPreviousCategoryId = -1;

        /// <summary>
        /// GenerateListingFromImagesAsync - builds a draft listing from the image slots.
        /// Main image decides Title, Description and CategoryId. Extras only enrich Description.
        /// </summary>
        public async Task<GeneratedListingDto?> GenerateListingFromImagesAsync(IEnumerable<IFormFile> imageFiles, CancellationToken cancellationToken)
        {
            try
            {
                if (imageFiles == null)
                {
                    return null;
                }

                List<IFormFile> validFiles = imageFiles.Where(f => f != null && f.Length > 0).ToList();

                if (validFiles.Count == 0)
                {
                    return null;
                }

                List<CategoryModel> categories = await LoadOrderedCategoriesAsync(cancellationToken);

                string categoryMappingString = BuildCategoryMappingString(categories);

                string rawBaseUrl = Environment.GetEnvironmentVariable("AI_API_URL") ?? "http://localhost:1234/v1";
                string rawApiKey = Environment.GetEnvironmentVariable("AI_API_KEY") ?? "lm-studio";
                string rawModel = Environment.GetEnvironmentVariable("AI_MODEL_NAME") ?? "Qwen3-VL-2B-Instruct-GGUF";

                string baseUrl = rawBaseUrl.Trim().Trim('"').Trim('\'').TrimEnd('/');
                string apiKey = rawApiKey.Trim().Trim('"').Trim('\'');
                string modelName = rawModel.Trim().Trim('"').Trim('\'');

                if (string.IsNullOrWhiteSpace(baseUrl))
                {
                    baseUrl = "http://localhost:1234/v1";
                }

                if (string.IsNullOrWhiteSpace(modelName))
                {
                    modelName = "Qwen3-VL-2B-Instruct-GGUF";
                }

                List<IFormFile> orderedFiles = validFiles.Take(MaxImages).ToList();

                List<PreparedImage> preparedImages = new List<PreparedImage>();

                for (int i = 0; i < orderedFiles.Count; i++)
                {
                    IFormFile file = orderedFiles[i];

                    if (file.Length > MaxImageBytes)
                    {
                        Console.WriteLine($"AI image skipped for slot {i + 1}: file too large ({file.Length} bytes)");
                        continue;
                    }

                    byte[]? imageBytes = null;

                    try
                    {
                        imageBytes = await Helper.GetByteArrayFromImage(file);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"AI image read failed for slot {i + 1}: {ex.Message}");
                        continue;
                    }

                    if (imageBytes == null)
                    {
                        continue;
                    }

                    if (imageBytes.Length == 0)
                    {
                        continue;
                    }

                    if (imageBytes.Length > MaxImageBytes)
                    {
                        Console.WriteLine($"AI image skipped for slot {i + 1}: decoded bytes too large ({imageBytes.Length} bytes)");
                        continue;
                    }

                    string mimeType;

                    if (!string.IsNullOrEmpty(file.ContentType))
                    {
                        mimeType = file.ContentType;
                    }
                    else
                    {
                        mimeType = "image/jpeg";
                    }

                    string base64Image = Convert.ToBase64String(imageBytes);

                    preparedImages.Add(new PreparedImage
                    {
                        Index = i,
                        MimeType = mimeType,
                        Base64 = base64Image,
                        FileName = file.FileName
                    });
                }

                if (preparedImages.Count == 0)
                {
                    Console.WriteLine("AI generation skipped: no readable images");
                    return null;
                }

                bool hasMainImage = preparedImages.Any(p => p.Index == 0);

                if (!hasMainImage)
                {
                    Console.WriteLine("AI generation skipped: main image could not be read");
                    return null;
                }

                string systemPrompt = BuildSystemPrompt(categoryMappingString);

                // Step 1: main image decides Title, Description and CategoryId. This is the previous working logic.
                PreparedImage mainImage = preparedImages.First(p => p.Index == 0);

                List<object> mainUserContent = new List<object>
                {
                    new
                    {
                        type = "text",
                        text = "Analyze this product image with absolute precision. Identify the exact item shown (e.g. if it is a refrigerator, use 'Хладилник'; if a bicycle, use 'Колело'). Do not hallucinate or mix up items."
                    },
                    new
                    {
                        type = "image_url",
                        image_url = new { url = $"data:{mainImage.MimeType};base64,{mainImage.Base64}" }
                    }
                };

                object mainPayload = BuildPayload(modelName, systemPrompt, mainUserContent.ToArray(), MaxTokensMain, 0.0);

                GeneratedListingDto? baseResult = await SendAiRequestAsync(baseUrl, apiKey, mainPayload, categories, cancellationToken);
                GeneratedListingDto? validatedBase = ValidateAndFix(baseResult, categories);

                if (validatedBase == null)
                {
                    Console.WriteLine("AI main image request failed validation or returned null");
                    return null;
                }

                // If category is still marked as keep previous, try a dedicated category-only call to get best match.
                if (validatedBase.CategoryId == KeepPreviousCategoryId)
                {
                    if (categories.Count > 0)
                    {
                        int fallbackCategory = await TrySelectBestCategoryViaAiAsync(baseUrl, apiKey, modelName, mainImage, categories, cancellationToken);

                        if (fallbackCategory != KeepPreviousCategoryId)
                        {
                            Console.WriteLine($"AI category fallback via dedicated prompt selected id {fallbackCategory}");
                            validatedBase.CategoryId = fallbackCategory;
                        }
                        else
                        {
                            int inferred = TryInferCategoryFromText(categories, validatedBase.Title, validatedBase.Description);

                            if (inferred != KeepPreviousCategoryId)
                            {
                                Console.WriteLine($"AI category fallback via inference selected id {inferred}");
                                validatedBase.CategoryId = inferred;
                            }
                            else if (categories.Count > 0)
                            {
                                // Final safety: choose first available category so the ad is still creatable.
                                // This is not hardcoded to a name, it is whatever the DB has first by Id.
                                Console.WriteLine($"AI category still invalid, using first available category id {categories[0].Id} as last resort");
                                validatedBase.CategoryId = categories[0].Id;
                            }
                        }
                    }
                }

                // Step 2: extra images enrich Description only.
                List<PreparedImage> extraImages = preparedImages.Where(p => p.Index != 0).ToList();

                if (extraImages.Count > 0)
                {
                    try
                    {
                        string? extraDetails = await ExtractExtraDetailsAsync(baseUrl, apiKey, modelName, extraImages, cancellationToken);

                        if (!string.IsNullOrWhiteSpace(extraDetails))
                        {
                            string merged = MergeDescriptions(validatedBase.Description, extraDetails);

                            if (merged.Length > 250)
                            {
                                merged = TruncateDescription(merged, 250);
                            }

                            validatedBase.Description = merged;
                            Console.WriteLine($"AI extra details merged: '{extraDetails}' -> final length {merged.Length}");
                        }
                        else
                        {
                            Console.WriteLine("AI extra details empty, keeping base description");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"AI extra details extraction failed, keeping base description: {ex.Message}");
                    }
                }

                return validatedBase;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AI generation top level error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// LoadOrderedCategoriesAsync - fetches all categories from the database ordered by Id.
        /// No names or ids are hardcoded. Returns empty list if DB is unavailable, never throws.
        /// </summary>
        private async Task<List<CategoryModel>> LoadOrderedCategoriesAsync(CancellationToken cancellationToken)
        {
            try
            {
                List<CategoryModel> categories = await _context.Categories
                    .AsNoTracking()
                    .OrderBy(c => c.Id)
                    .ToListAsync(cancellationToken);

                if (categories.Count == 0)
                {
                    Console.WriteLine("AI categories: no categories found in database");
                }
                else
                {
                    Console.WriteLine($"AI categories loaded: {string.Join(", ", categories.Select(c => $"{c.Id}:{c.Name}"))}");
                }

                return categories;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AI categories load failed: {ex.Message}");
                return new List<CategoryModel>();
            }
        }

        private static string BuildCategoryMappingString(List<CategoryModel> categories)
        {
            if (categories == null || categories.Count == 0)
            {
                return "No categories available";
            }

            return string.Join(", ", categories.Select(c => $"{c.Id}: {c.Name}"));
        }

        private static string BuildSystemPrompt(string categoryMappingString)
        {
            string prompt = "You are an expert image analyzer for online marketplaces. "
                + "Return ONLY a valid raw JSON object with NO markdown formatting, NO backticks, and NO conversational text. "
                + "Your response must strictly follow this exact JSON structure:\n"
                + "{\n"
                + "  \"Title\": \"Catchy title in native English, 3-35 chars, include color/feature, keep brand/model in Latin\",\n"
                + "  \"Description\": \"Condition: [Perfect/Good/Bad]\\nDetails: [Color, material or characteristics]\",\n"
                + "  \"CategoryId\": 1\n"
                + "}\n\n"
                + $"Choose 'CategoryId' strictly as an integer from these active database categories: [{categoryMappingString}]";

            return prompt;
        }

        private static object BuildPayload(string modelName, string systemPrompt, object[] contentParts, int maxTokens, double temperature)
        {
            object payload = new
            {
                model = modelName,
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = contentParts }
                },
                temperature = temperature,
                max_tokens = maxTokens
            };

            return payload;
        }

        private async Task<string?> ExtractExtraDetailsAsync(string baseUrl, string apiKey, string modelName, List<PreparedImage> extraImages, CancellationToken cancellationToken)
        {
            if (extraImages == null)
            {
                return null;
            }

            if (extraImages.Count == 0)
            {
                return null;
            }

            string systemPrompt = "You are an expert image analyzer for online marketplaces. "
                + "Return ONLY a valid raw JSON object with NO markdown, NO backticks, NO conversational text. "
                + "Your response must strictly follow this JSON structure:\n"
                + "{\n"
                + "  \"ExtraDetails\": \"string\"\n"
                + "}\n\n"
                + "ExtraDetails: 10 to 120 characters, English, describe only what is seen in the extra images that supplements the main product. "
                + "Include real color, material, visible defects, accessories, angles or condition details. "
                + "Do not repeat generic phrases. Do not mention Title or Category. If nothing new is visible, return an empty string.";

            List<object> parts = new List<object>();

            parts.Add(new
            {
                type = "text",
                text = "These are additional detail images of the SAME product shown in the main image. "
                    + "Extract only supplementary facts for the Description. "
                    + "Look for color nuances, material, texture, wear, scratches, dents, missing parts, accessories, labels or packaging that were not clear in the main view. "
                    + "Be concise and factual."
            });

            for (int i = 0; i < extraImages.Count; i++)
            {
                PreparedImage img = extraImages[i];

                parts.Add(new { type = "text", text = $"Additional image {i + 1}:" });
                parts.Add(new { type = "image_url", image_url = new { url = $"data:{img.MimeType};base64,{img.Base64}" } });
            }

            object payload = BuildPayload(modelName, systemPrompt, parts.ToArray(), MaxTokensExtra, 0.0);

            try
            {
                string? rawContent = await SendRawContentAsync(baseUrl, apiKey, payload, cancellationToken);

                if (string.IsNullOrWhiteSpace(rawContent))
                {
                    return null;
                }

                string cleaned = ExtractJsonObject(rawContent);

                if (string.IsNullOrWhiteSpace(cleaned))
                {
                    cleaned = rawContent.Trim();
                }

                using JsonDocument doc = JsonDocument.Parse(cleaned);
                JsonElement root = doc.RootElement;

                if (root.TryGetProperty("ExtraDetails", out JsonElement extraProp))
                {
                    string? value = extraProp.GetString();

                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        string trimmed = value.Trim();

                        if (trimmed.Length > 120)
                        {
                            trimmed = trimmed.Substring(0, 120).Trim();
                        }

                        return trimmed;
                    }
                }

                if (root.TryGetProperty("Description", out JsonElement descProp))
                {
                    string? desc = descProp.GetString();

                    if (!string.IsNullOrWhiteSpace(desc))
                    {
                        return desc.Trim();
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AI extra details parse failed: {ex.Message}");
                return null;
            }
        }

        private static string MergeDescriptions(string baseDescription, string extraDetails)
        {
            if (string.IsNullOrWhiteSpace(baseDescription))
            {
                baseDescription = "Condition: Good\nDetails: Product as shown in main image.";
            }

            if (string.IsNullOrWhiteSpace(extraDetails))
            {
                return baseDescription.Trim();
            }

            string extra = extraDetails.Trim().TrimEnd('.', ';', ',');
            string normalizedBase = baseDescription.Trim();
            string[] lines = normalizedBase.Split('\n');

            if (lines.Length >= 2)
            {
                string firstLine = lines[0].Trim();
                string secondLine = string.Join(" ", lines.Skip(1)).Trim();

                if (!secondLine.StartsWith("Details:", StringComparison.OrdinalIgnoreCase))
                {
                    secondLine = "Details: " + secondLine;
                }

                string mergedSecond = secondLine.TrimEnd('.', ';') + "; " + extra;
                string merged = firstLine + "\n" + mergedSecond;
                return merged.Trim();
            }
            else
            {
                string merged = normalizedBase.TrimEnd('.', ';') + "; " + extra;
                return merged.Trim();
            }
        }

        private static string TruncateDescription(string description, int maxLength)
        {
            if (string.IsNullOrEmpty(description))
            {
                return description;
            }

            if (description.Length <= maxLength)
            {
                return description;
            }

            string truncated = description.Substring(0, maxLength).Trim();
            int lastSeparator = truncated.LastIndexOf(';');

            if (lastSeparator > maxLength * 0.6)
            {
                truncated = truncated.Substring(0, lastSeparator).Trim();
            }
            else
            {
                int lastSpace = truncated.LastIndexOf(' ');

                if (lastSpace > maxLength * 0.7)
                {
                    truncated = truncated.Substring(0, lastSpace).Trim();
                }
            }

            return truncated;
        }

        private static GeneratedListingDto? ValidateAndFix(GeneratedListingDto? result, List<CategoryModel> categories)
        {
            if (result == null)
            {
                return null;
            }

            result.Title = (result.Title ?? string.Empty).Trim();
            result.Description = (result.Description ?? string.Empty).Trim();

            // Check for placeholder brackets. The model sometimes returns Condition: [Perfect] with brackets
            // even though the prompt says no brackets. We try to clean them first and only reject if the
            // cleaned result is still a placeholder template.
            bool hasBracket = result.Title.Contains('[') || result.Title.Contains(']') || result.Description.Contains('[') || result.Description.Contains(']');

            if (hasBracket)
            {
                string cleanedTitleForCheck = result.Title.Replace("[", "").Replace("]", "").Trim();
                string cleanedDescForCheck = result.Description.Replace("[", "").Replace("]", "").Trim();

                bool isPlaceholderTemplate = cleanedDescForCheck.Contains("Color, material or characteristics", StringComparison.OrdinalIgnoreCase)
                    || cleanedDescForCheck.Contains("Color, material", StringComparison.OrdinalIgnoreCase)
                    || cleanedTitleForCheck.Contains("Color, material", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(cleanedDescForCheck, "Details: Color, material or characteristics", StringComparison.OrdinalIgnoreCase);

                if (isPlaceholderTemplate)
                {
                    Console.WriteLine($"AI result is placeholder template: Title='{result.Title}' Description='{result.Description}'");
                    return null;
                }

                // Not a template, just stray brackets. Clean them and continue.
                Console.WriteLine($"AI result had stray brackets, cleaned Title='{cleanedTitleForCheck}' Description='{cleanedDescForCheck}'");

                result.Title = cleanedTitleForCheck;
                result.Description = cleanedDescForCheck;
            }

            if (string.IsNullOrWhiteSpace(result.Title))
            {
                return null;
            }

            if (result.Title.Length < 3)
            {
                return null;
            }

            if (result.Title.Length > 35)
            {
                result.Title = result.Title.Substring(0, 35).Trim();
            }

            if (string.IsNullOrWhiteSpace(result.Description))
            {
                return null;
            }

            if (result.Description.Length < 20)
            {
                return null;
            }

            if (result.Description.Length > 250)
            {
                result.Description = TruncateDescription(result.Description, 250);
            }

            if (!result.Description.Contains('\n'))
            {
                if (!result.Description.StartsWith("Condition:", StringComparison.OrdinalIgnoreCase)
                    && !result.Description.StartsWith("Details:", StringComparison.OrdinalIgnoreCase))
                {
                    result.Description = "Condition: Good\nDetails: " + result.Description;
                }
            }

            bool categoryIsValid = IsValidCategoryId(result.CategoryId, categories);

            if (!categoryIsValid)
            {
                int inferredId = TryInferCategoryFromText(categories, result.Title, result.Description);

                if (inferredId != KeepPreviousCategoryId)
                {
                    Console.WriteLine($"AI CategoryId {result.CategoryId} invalid, inferred category id {inferredId} from title/description");
                    result.CategoryId = inferredId;
                    categoryIsValid = true;
                }
            }

            if (!categoryIsValid)
            {
                if (categories.Count == 0)
                {
                    Console.WriteLine($"AI CategoryId {result.CategoryId} is invalid and no categories exist, keeping as -1");

                    if (result.CategoryId == 0)
                    {
                        result.CategoryId = KeepPreviousCategoryId;
                    }
                }
                else
                {
                    Console.WriteLine($"AI CategoryId {result.CategoryId} is invalid, marking for category-only fallback (CategoryId={KeepPreviousCategoryId})");
                    result.CategoryId = KeepPreviousCategoryId;
                }
            }

            return result;
        }

        private static bool IsValidCategoryId(int categoryId, List<CategoryModel> categories)
        {
            if (categories == null)
            {
                return false;
            }

            if (categories.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < categories.Count; i++)
            {
                if (categories[i].Id == categoryId)
                {
                    return true;
                }
            }

            return false;
        }

        private static int TryInferCategoryFromText(List<CategoryModel> categories, string title, string description)
        {
            if (categories == null)
            {
                return KeepPreviousCategoryId;
            }

            if (categories.Count == 0)
            {
                return KeepPreviousCategoryId;
            }

            string combinedText = $"{title} {description}".ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(combinedText))
            {
                return KeepPreviousCategoryId;
            }

            for (int i = 0; i < categories.Count; i++)
            {
                CategoryModel cat = categories[i];
                string lowerCatName = cat.Name.ToLowerInvariant();

                if (string.IsNullOrWhiteSpace(lowerCatName))
                {
                    continue;
                }

                string[] catWords = lowerCatName.Split(new[] { ' ', '&', '-', '/' }, StringSplitOptions.RemoveEmptyEntries);

                for (int w = 0; w < catWords.Length; w++)
                {
                    string word = catWords[w].Trim();

                    if (word.Length < 3)
                    {
                        continue;
                    }

                    if (combinedText.Contains(word))
                    {
                        return cat.Id;
                    }
                }
            }

            return KeepPreviousCategoryId;
        }

        private static int TryMapCategoryNameToId(List<CategoryModel> categories, string? categoryName)
        {
            if (categories == null)
            {
                return KeepPreviousCategoryId;
            }

            if (categories.Count == 0)
            {
                return KeepPreviousCategoryId;
            }

            if (string.IsNullOrWhiteSpace(categoryName))
            {
                return KeepPreviousCategoryId;
            }

            string trimmedName = categoryName.Trim();

            if (string.IsNullOrWhiteSpace(trimmedName))
            {
                return KeepPreviousCategoryId;
            }

            for (int i = 0; i < categories.Count; i++)
            {
                CategoryModel cat = categories[i];

                if (string.Equals(cat.Name, trimmedName, StringComparison.OrdinalIgnoreCase))
                {
                    return cat.Id;
                }
            }

            for (int i = 0; i < categories.Count; i++)
            {
                CategoryModel cat = categories[i];

                if (trimmedName.IndexOf(cat.Name, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return cat.Id;
                }

                if (cat.Name.IndexOf(trimmedName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return cat.Id;
                }
            }

            return KeepPreviousCategoryId;
        }

        private async Task<int> TrySelectBestCategoryViaAiAsync(string baseUrl, string apiKey, string modelName, PreparedImage mainImage, List<CategoryModel> categories, CancellationToken cancellationToken)
        {
            if (categories == null)
            {
                return KeepPreviousCategoryId;
            }

            if (categories.Count == 0)
            {
                return KeepPreviousCategoryId;
            }

            string categoryList = string.Join(", ", categories.Select(c => $"{c.Id}: {c.Name}"));

            string systemPrompt = "You are an expert categorizer for online marketplaces. "
                + "Return ONLY a valid raw JSON object with NO markdown. "
                + "Your response must be {\"CategoryId\": 1} where CategoryId is an integer from this list: ["
                + categoryList + "] "
                + "Pick the single best matching category for the product shown. Do not invent a new id.";

            List<object> userContent = new List<object>
            {
                new { type = "text", text = "Pick the best category for this product image. Return only the JSON with CategoryId." },
                new { type = "image_url", image_url = new { url = $"data:{mainImage.MimeType};base64,{mainImage.Base64}" } }
            };

            object payload = BuildPayload(modelName, systemPrompt, userContent.ToArray(), 100, 0.0);

            try
            {
                GeneratedListingDto? result = await SendAiRequestAsync(baseUrl, apiKey, payload, categories, cancellationToken);

                if (result != null)
                {
                    if (IsValidCategoryId(result.CategoryId, categories))
                    {
                        return result.CategoryId;
                    }
                }

                string? raw = await SendRawContentAsync(baseUrl, apiKey, payload, cancellationToken);

                if (!string.IsNullOrWhiteSpace(raw))
                {
                    string cleaned = ExtractJsonObject(raw);

                    if (!string.IsNullOrWhiteSpace(cleaned))
                    {
                        GeneratedListingDto? manual = TryManualExtractListing(cleaned, out string? extractedName);

                        if (manual != null)
                        {
                            if (IsValidCategoryId(manual.CategoryId, categories))
                            {
                                return manual.CategoryId;
                            }

                            if (!string.IsNullOrWhiteSpace(extractedName))
                            {
                                int mapped = TryMapCategoryNameToId(categories, extractedName);

                                if (mapped != KeepPreviousCategoryId)
                                {
                                    return mapped;
                                }
                            }
                        }
                    }
                }

                return KeepPreviousCategoryId;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AI category-only fallback failed: {ex.Message}");
                return KeepPreviousCategoryId;
            }
        }

        private async Task<GeneratedListingDto?> SendAiRequestAsync(string baseUrl, string apiKey, object payload, List<CategoryModel> categories, CancellationToken cancellationToken)
        {
            try
            {
                string? rawContent = await SendRawContentAsync(baseUrl, apiKey, payload, cancellationToken);

                if (string.IsNullOrWhiteSpace(rawContent))
                {
                    return null;
                }

                string cleanedJson = ExtractJsonObject(rawContent);

                if (string.IsNullOrWhiteSpace(cleanedJson))
                {
                    cleanedJson = rawContent.Trim();
                }

                JsonSerializerOptions options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
                };

                GeneratedListingDto? dto = null;

                try
                {
                    dto = JsonSerializer.Deserialize<GeneratedListingDto>(cleanedJson, options);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"AI strict JSON parse failed, will try manual extraction: {ex.Message} raw='{cleanedJson}'");
                }

                if (dto == null || dto.CategoryId == 0)
                {
                    GeneratedListingDto? manual = TryManualExtractListing(cleanedJson, out string? extractedName);

                    if (manual != null)
                    {
                        if (dto == null)
                        {
                            dto = manual;
                        }
                        else
                        {
                            if (manual.CategoryId != 0)
                            {
                                dto.CategoryId = manual.CategoryId;
                            }

                            if (string.IsNullOrWhiteSpace(dto.Title) && !string.IsNullOrWhiteSpace(manual.Title))
                            {
                                dto.Title = manual.Title;
                            }

                            if (string.IsNullOrWhiteSpace(dto.Description) && !string.IsNullOrWhiteSpace(manual.Description))
                            {
                                dto.Description = manual.Description;
                            }
                        }

                        if (dto != null && dto.CategoryId == 0 && !string.IsNullOrWhiteSpace(extractedName) && categories != null && categories.Count > 0)
                        {
                            int mappedId = TryMapCategoryNameToId(categories, extractedName);

                            if (mappedId != KeepPreviousCategoryId)
                            {
                                dto.CategoryId = mappedId;
                                Console.WriteLine($"AI category name '{extractedName}' mapped to id {dto.CategoryId} inside SendAiRequestAsync");
                            }
                        }
                    }
                }

                return dto;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AI Generation Error: {ex.Message}");
                return null;
            }
        }

        private Task<GeneratedListingDto?> SendAiRequestAsync(string baseUrl, string apiKey, object payload, CancellationToken cancellationToken)
        {
            return SendAiRequestAsync(baseUrl, apiKey, payload, new List<CategoryModel>(), cancellationToken);
        }

        private static GeneratedListingDto? TryManualExtractListing(string cleanedJson, out string? extractedCategoryName)
        {
            extractedCategoryName = null;

            try
            {
                using JsonDocument doc = JsonDocument.Parse(cleanedJson);
                JsonElement root = doc.RootElement;

                if (root.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }

                string? title = null;
                string? description = null;
                int categoryId = 0;
                string? categoryName = null;

                foreach (JsonProperty prop in root.EnumerateObject())
                {
                    string key = prop.Name.ToLowerInvariant();

                    if (key == "title")
                    {
                        if (prop.Value.ValueKind == JsonValueKind.String)
                        {
                            title = prop.Value.GetString();
                        }
                    }
                    else if (key == "description")
                    {
                        if (prop.Value.ValueKind == JsonValueKind.String)
                        {
                            description = prop.Value.GetString();
                        }
                    }
                    else if (key == "categoryid" || key == "category_id" || key == "categoryid_" || key == "id")
                    {
                        if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetInt32(out int num))
                        {
                            categoryId = num;
                        }
                        else if (prop.Value.ValueKind == JsonValueKind.String)
                        {
                            string? s = prop.Value.GetString();

                            if (!string.IsNullOrWhiteSpace(s) && int.TryParse(s.Trim(), out int parsed))
                            {
                                categoryId = parsed;
                            }
                            else if (!string.IsNullOrWhiteSpace(s))
                            {
                                categoryName = s.Trim();
                            }
                        }
                    }
                    else if (key == "category" || key == "categoryname" || key == "category_name")
                    {
                        if (prop.Value.ValueKind == JsonValueKind.String)
                        {
                            categoryName = prop.Value.GetString()?.Trim();
                        }
                        else if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetInt32(out int num2))
                        {
                            categoryId = num2;
                        }
                    }
                }

                extractedCategoryName = categoryName;

                if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(description) && categoryId == 0 && string.IsNullOrWhiteSpace(categoryName))
                {
                    return null;
                }

                GeneratedListingDto result = new GeneratedListingDto
                {
                    Title = title ?? string.Empty,
                    Description = description ?? string.Empty,
                    CategoryId = categoryId
                };

                return result;
            }
            catch
            {
                extractedCategoryName = null;
                return null;
            }
        }

        private static GeneratedListingDto? TryManualExtractListing(string cleanedJson)
        {
            return TryManualExtractListing(cleanedJson, out _);
        }

        private async Task<string?> SendRawContentAsync(string baseUrl, string apiKey, object payload, CancellationToken cancellationToken)
        {
            string json = JsonSerializer.Serialize(payload);
            StringContent jsonContent = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
                request.Content = jsonContent;
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                // Every request is a new chat - no history is sent, only the system and single user message in payload.
                HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"AI request failed with status {response.StatusCode}");
                    return null;
                }

                string responseString = await response.Content.ReadAsStringAsync(cancellationToken);

                using JsonDocument doc = JsonDocument.Parse(responseString);
                JsonElement root = doc.RootElement;

                if (!root.TryGetProperty("choices", out JsonElement choices))
                {
                    return null;
                }

                if (choices.GetArrayLength() == 0)
                {
                    return null;
                }

                JsonElement firstChoice = choices[0];

                if (!firstChoice.TryGetProperty("message", out JsonElement message))
                {
                    return null;
                }

                if (!message.TryGetProperty("content", out JsonElement content))
                {
                    return null;
                }

                string? messageContent = content.GetString();

                if (string.IsNullOrWhiteSpace(messageContent))
                {
                    return null;
                }

                return messageContent;
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("AI request was canceled");
                return null;
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"AI request network error: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AI raw content error: {ex.Message}");
                return null;
            }
        }

        private static string ExtractJsonObject(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return string.Empty;
            }

            string trimmed = content.Trim();

            if (trimmed.StartsWith("```"))
            {
                int firstNewline = trimmed.IndexOf('\n');

                if (firstNewline != -1)
                {
                    int lastBackticks = trimmed.LastIndexOf("```");

                    if (lastBackticks > firstNewline)
                    {
                        trimmed = trimmed.Substring(firstNewline + 1, lastBackticks - (firstNewline + 1)).Trim();
                    }
                    else
                    {
                        trimmed = trimmed.Substring(firstNewline + 1).Trim();
                    }
                }

                trimmed = trimmed.Trim();

                if (trimmed.StartsWith("json", StringComparison.OrdinalIgnoreCase))
                {
                    trimmed = trimmed.Substring(4).Trim();
                }
            }

            if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
            {
                return trimmed;
            }

            try
            {
                int firstBrace = trimmed.IndexOf('{');
                int lastBrace = trimmed.LastIndexOf('}');

                if (firstBrace != -1 && lastBrace != -1 && lastBrace > firstBrace)
                {
                    string candidate = trimmed.Substring(firstBrace, lastBrace - firstBrace + 1).Trim();

                    try
                    {
                        using JsonDocument testDoc = JsonDocument.Parse(candidate);
                        return candidate;
                    }
                    catch
                    {
                    }
                }

                Match match = Regex.Match(trimmed, @"\{[\s\S]*?\}", RegexOptions.Multiline);

                if (match.Success)
                {
                    string matchValue = match.Value.Trim();

                    try
                    {
                        using JsonDocument testDoc2 = JsonDocument.Parse(matchValue);
                        return matchValue;
                    }
                    catch
                    {
                        return trimmed;
                    }
                }
            }
            catch
            {
                return trimmed;
            }

            return trimmed;
        }

        private sealed class PreparedImage
        {
            public int Index { get; set; }
            public string MimeType { get; set; } = "image/jpeg";
            public string Base64 { get; set; } = string.Empty;
            public string FileName { get; set; } = string.Empty;
        }
    }
}
