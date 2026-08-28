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
using System.Threading;
using System.Threading.Tasks;

namespace Marketplace.Services
{
    // AiService - talks to the AI vision API and turns images into a draft ad.
    public class AiService(HttpClient httpClient, ApplicationDbContext context) : IAiService
    {
        private readonly HttpClient _httpClient = httpClient;
        private readonly ApplicationDbContext _context = context;

        // GenerateListingFromImagesAsync - sends the main image to AI and parses the JSON reply.
        public async Task<GeneratedListingDto?> GenerateListingFromImagesAsync(IEnumerable<IFormFile> imageFiles, CancellationToken cancellationToken)
        {
            var validFiles = imageFiles?.Where(f => f != null && f.Length > 0).ToList();

            if (validFiles == null || validFiles.Count == 0)
            {
                return null;
            }

            // Load categories from DB to give the model valid choices.

            var categories = await _context.Categories.ToListAsync(cancellationToken);

            string categoryMappingString = categories.Any()
                ? string.Join(", ", categories.Select(c => $"{c.Id}: {c.Name}"))
                : "No categories available";

            string baseUrl = Environment.GetEnvironmentVariable("AI_API_URL") ?? "http://localhost:1234/v1";
            string apiKey = Environment.GetEnvironmentVariable("AI_API_KEY") ?? "lm-studio";
            string modelName = Environment.GetEnvironmentVariable("AI_MODEL_NAME") ?? "qwen";

            // Use only the first image to keep tokens low and avoid mix-ups.

            var primaryFile = validFiles.First();

            byte[] imageBytes = await Helper.GetByteArrayFromImage(primaryFile);
            string base64Image = Convert.ToBase64String(imageBytes);

            string mimeType = !string.IsNullOrEmpty(primaryFile.ContentType) ? primaryFile.ContentType : "image/jpeg";

            var userContentList = new object[]
            {
                new
                {
                    type = "text",
                    text = "Analyze this product image with absolute precision. Identify the exact item shown (e.g. if it is a refrigerator, use 'Хладилник'; if a bicycle, use 'Колело'). Do not hallucinate or mix up items."
                },
                new
                {
                    type = "image_url",
                    image_url = new { url = $"data:{mimeType};base64,{base64Image}" }
                }
            };

            var requestPayload = new
            {
                model = modelName,
                messages = new object[]
                {
                    new
                    {
                        role = "system",
                        content = "You are an expert image analyzer for online marketplaces. " +
                                   "Return ONLY a valid raw JSON object with NO markdown formatting, NO backticks, and NO conversational text. " +
                                   "Your response must strictly follow this exact JSON structure:\n" +
                                   "{\n" +
                                   "  \"Title\": \"Catchy title in native English, 3-35 chars, include color/feature, keep brand/model in Latin\",\n" +
                                   "  \"Description\": \"Condition: [Perfect/Good/Bad]\\nDetails: [Color, material or characteristics]\",\n" +
                                   "  \"CategoryId\": 1\n" +
                                   "}\n\n" +
                                   $"Choose 'CategoryId' strictly as an integer from these active database categories: [{categoryMappingString}]"
                    },
                    new
                    {
                        role = "user",
                        content = userContentList
                    }
                },
                temperature = 0.0,
                max_tokens = 300
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(requestPayload), Encoding.UTF8, "application/json");

            try
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                var response = await _httpClient.PostAsync($"{baseUrl}/chat/completions", jsonContent, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

                using var doc = JsonDocument.Parse(responseString);

                var root = doc.RootElement;

                if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                {
                    return null;
                }

                var messageContent = choices[0].GetProperty("message").GetProperty("content").GetString();

                if (string.IsNullOrWhiteSpace(messageContent))
                {
                    return null;
                }

                // Clean markdown fences if the model wrapped the JSON anyway.

                string cleanedJson = messageContent.Trim();

                if (cleanedJson.StartsWith("```"))
                {
                    int firstNewline = cleanedJson.IndexOf('\n');
                    int lastBackticks = cleanedJson.LastIndexOf("```");

                    if (firstNewline != -1 && lastBackticks > firstNewline)
                    {
                        cleanedJson = cleanedJson.Substring(firstNewline + 1, lastBackticks - (firstNewline + 1)).Trim();
                    }
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                return JsonSerializer.Deserialize<GeneratedListingDto>(cleanedJson, options);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AI Generation Error: {ex.Message}");

                return null;
            }
        }
    }
}
