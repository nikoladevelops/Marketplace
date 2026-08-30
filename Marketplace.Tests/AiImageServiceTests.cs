using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Marketplace.Models;
using Marketplace.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Marketplace.Tests
{
    /// <summary>
    /// AiImageService tests - ensures every generation is a new isolated chat and that Title, Description and Category are all selected.
    /// The main image (slot 1) decides Title and CategoryId and base Description. Extra images (slots 2-4) only enrich Description.
    /// Tests also verify friendly handling when the model does not recognize a photo - the service returns null and the controller
    /// shows a gentle popup, never a scary technical trace. Real details go to server and browser console.
    /// </summary>
    [TestFixture]
    public class AiImageServiceTests
    {
        private ApplicationDbContext _context = null!;
        private byte[] _tinyJpeg = null!;

        [SetUp]
        public async Task SetUp()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);

            string[] seedCats = new[]
            {
                "Furniture",
                "Home Appliances",
                "Fashion & Accessories",
                "Smartphones",
                "Computers & Laptops",
                "Audio & Headphones",
                "TV & Home Entertainment",
                "Cameras & Photography",
                "Sports & Outdoors"
            };

            for (int i = 0; i < seedCats.Length; i++)
            {
                _context.Categories.Add(new CategoryModel { Id = i + 1, Name = seedCats[i] });
            }

            await _context.SaveChangesAsync();

            _tinyJpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01 };
        }

        [TearDown]
        public void TearDown()
        {
            _context.Dispose();
        }

        private static IFormFile MakeFile(string name, string contentType, byte[] bytes)
        {
            var stream = new MemoryStream(bytes);
            var file = new FormFile(stream, 0, bytes.Length, "images", name)
            {
                Headers = new HeaderDictionary(),
                ContentType = contentType
            };

            return file;
        }

        private static HttpResponseMessage JsonResponse(string innerJson)
        {
            var payload = new { choices = new[] { new { message = new { content = innerJson } } } };
            string json = JsonSerializer.Serialize(payload);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }

        private class MockHandler : HttpMessageHandler
        {
            private readonly Queue<HttpResponseMessage> _responses = new();

            public List<HttpRequestMessage> Captured { get; } = new();

            public void Enqueue(HttpResponseMessage response)
            {
                _responses.Enqueue(response);
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Captured.Add(request);

                if (_responses.Count > 0)
                {
                    return Task.FromResult(_responses.Dequeue());
                }

                string fallback = JsonSerializer.Serialize(new
                {
                    choices = new[] { new { message = new { content = "{\"Title\":\"Fallback\",\"Description\":\"Condition: Good\\nDetails: Fallback description that is long enough to be valid.\",\"CategoryId\":1}" } } }
                });

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(fallback, Encoding.UTF8, "application/json")
                });
            }

            public int ImageCountInLastPayload()
            {
                if (Captured.Count == 0)
                {
                    return 0;
                }

                string body = Captured.Last().Content!.ReadAsStringAsync().Result;
                int count = 0;
                int idx = 0;

                while ((idx = body.IndexOf("image_url", idx, StringComparison.Ordinal)) != -1)
                {
                    count++;
                    idx += 9;
                }

                return count / 2;
            }
        }

        [Test]
        public async Task GenerateListingFromImagesAsync_NullInput_ReturnsNull()
        {
            var handler = new MockHandler();
            var service = new AiImageService(new HttpClient(handler), _context);

            var result = await service.GenerateListingFromImagesAsync(null!, CancellationToken.None);

            result.Should().BeNull();
        }

        [Test]
        public async Task GenerateListingFromImagesAsync_EmptyList_ReturnsNull()
        {
            var handler = new MockHandler();
            var service = new AiImageService(new HttpClient(handler), _context);

            var result = await service.GenerateListingFromImagesAsync(new List<IFormFile>(), CancellationToken.None);

            result.Should().BeNull();
        }

        [Test]
        public async Task GenerateListingFromImagesAsync_MarkdownFence_IsParsed()
        {
            var handler = new MockHandler();
            handler.Enqueue(JsonResponse("```json\n{\"Title\":\"Vintage Guitar\",\"Description\":\"Condition: Perfect\\nDetails: Sunburst, wood, no scratches\",\"CategoryId\":6}\n```"));

            var service = new AiImageService(new HttpClient(handler), _context);
            var file = MakeFile("main.jpg", "image/jpeg", _tinyJpeg);

            var result = await service.GenerateListingFromImagesAsync(new[] { file }, CancellationToken.None);

            result.Should().NotBeNull();
            result!.Title.Should().Be("Vintage Guitar");
            result.CategoryId.Should().Be(6);
        }

        [Test]
        public async Task GenerateListingFromImagesAsync_StrayBrackets_AreCleaned()
        {
            var handler = new MockHandler();
            handler.Enqueue(JsonResponse("{\"Title\":\"iPhone 14 Pro\",\"Description\":\"Condition: [Perfect]\\nDetails: Black, glass\",\"CategoryId\":4}"));

            var service = new AiImageService(new HttpClient(handler), _context);
            var file = MakeFile("main.jpg", "image/jpeg", _tinyJpeg);

            var result = await service.GenerateListingFromImagesAsync(new[] { file }, CancellationToken.None);

            result.Should().NotBeNull();
            result!.Title.Should().Be("iPhone 14 Pro");
            result.Description.Should().Contain("Condition: Perfect");
            result.Description.Should().NotContain("[");
        }

        [Test]
        public async Task GenerateListingFromImagesAsync_PlaceholderTemplate_IsRejectedAsNotRecognized()
        {
            var handler = new MockHandler();
            handler.Enqueue(JsonResponse("{\"Title\":\"Cathedral of Roses\",\"Description\":\"Condition: [Perfect]\\nDetails: [Color, material or characteristics]\",\"CategoryId\":1}"));

            var service = new AiImageService(new HttpClient(handler), _context);
            var file = MakeFile("main.jpg", "image/jpeg", _tinyJpeg);

            var result = await service.GenerateListingFromImagesAsync(new[] { file }, CancellationToken.None);

            // Placeholder template means the model did not recognize the photo. Service returns null so controller shows friendly popup.
            result.Should().BeNull();
        }

        [Test]
        public async Task GenerateListingFromImagesAsync_CategoryName_IsMappedToId()
        {
            var handler = new MockHandler();
            handler.Enqueue(JsonResponse("{\"Title\":\"Sofa\",\"Description\":\"Condition: Good\\nDetails: Nice sofa\",\"Category\":\"Furniture\"}"));

            var service = new AiImageService(new HttpClient(handler), _context);
            var file = MakeFile("main.jpg", "image/jpeg", _tinyJpeg);

            var result = await service.GenerateListingFromImagesAsync(new[] { file }, CancellationToken.None);

            result.Should().NotBeNull();
            result!.CategoryId.Should().Be(1);
        }

        [Test]
        public async Task GenerateListingFromImagesAsync_StringCategoryId_IsParsed()
        {
            var handler = new MockHandler();
            handler.Enqueue(JsonResponse("{\"Title\":\"Laptop\",\"Description\":\"Condition: Good\\nDetails: Fast\",\"CategoryId\":\"5\"}"));

            var service = new AiImageService(new HttpClient(handler), _context);
            var file = MakeFile("main.jpg", "image/jpeg", _tinyJpeg);

            var result = await service.GenerateListingFromImagesAsync(new[] { file }, CancellationToken.None);

            result.Should().NotBeNull();
            result!.CategoryId.Should().Be(5);
        }

        [Test]
        public async Task GenerateListingFromImagesAsync_ExtraImages_EnrichDescriptionOnly()
        {
            var handler = new MockHandler();
            handler.Enqueue(JsonResponse("{\"Title\":\"MainProduct\",\"Description\":\"Condition: Good\\nDetails: White metal, small dent\",\"CategoryId\":2}"));
            handler.Enqueue(JsonResponse("{\"ExtraDetails\":\"side scratch and ice maker visible\"}"));

            var service = new AiImageService(new HttpClient(handler), _context);
            var main = MakeFile("main.jpg", "image/jpeg", _tinyJpeg);
            var extra = MakeFile("extra.jpg", "image/jpeg", _tinyJpeg);

            var result = await service.GenerateListingFromImagesAsync(new[] { main, extra }, CancellationToken.None);

            result.Should().NotBeNull();
            result!.Title.Should().Be("MainProduct");
            result.CategoryId.Should().Be(2);
            result.Description.Should().Contain("side scratch");

            // Isolation check: first chat had 1 image (main), second chat had 1 image (extra), never mixed.
            handler.Captured.Count.Should().Be(2);
            handler.ImageCountInLastPayload().Should().Be(1);
            string firstBody = handler.Captured[0].Content!.ReadAsStringAsync().Result;
            int firstCount = (firstBody.Split("image_url").Length - 1) / 2;
            firstCount.Should().Be(1);
        }

        [Test]
        public async Task GenerateListingFromImagesAsync_Offline_ReturnsNullForFriendlyPopup()
        {
            var handler = new MockHandler();
            handler.Enqueue(new HttpResponseMessage(HttpStatusCode.InternalServerError));
            handler.Enqueue(new HttpResponseMessage(HttpStatusCode.InternalServerError));

            var service = new AiImageService(new HttpClient(handler), _context);
            var file = MakeFile("main.jpg", "image/jpeg", _tinyJpeg);

            var result = await service.GenerateListingFromImagesAsync(new[] { file }, CancellationToken.None);

            result.Should().BeNull();
        }

        [Test]
        public async Task GenerateListingFromImagesAsync_ConcurrentCalls_AreIsolated()
        {
            var handler = new MockHandler();
            // Enqueue enough responses for 5 concurrent calls
            for (int i = 0; i < 10; i++)
            {
                handler.Enqueue(JsonResponse($"{{\"Title\":\"Item {i}\",\"Description\":\"Condition: Good\\nDetails: Descr {i} long enough to be valid\",\"CategoryId\":{(i % 9) + 1}}}"));
            }

            var service = new AiImageService(new HttpClient(handler), _context);
            var tasks = new List<Task<GeneratedListingDto?>>();

            for (int i = 0; i < 5; i++)
            {
                var file = MakeFile($"main{i}.jpg", "image/jpeg", _tinyJpeg);
                tasks.Add(service.GenerateListingFromImagesAsync(new[] { file }, CancellationToken.None));
            }

            var results = await Task.WhenAll(tasks);

            foreach (var r in results)
            {
                r.Should().NotBeNull();
                r!.CategoryId.Should().BeGreaterThan(0);
            }

            // Each call should have been a separate chat with 1 image
            handler.Captured.Count.Should().Be(5);
        }

        [Test]
        public async Task GenerateListingFromImagesAsync_15MockedCategories_AllGetBestMatch()
        {
            var handler = new MockHandler();

            // Simulate 15 different products, each with expected category
            var expectedCategories = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 1, 2, 3, 4, 5, 6 };

            for (int i = 0; i < 15; i++)
            {
                int cat = expectedCategories[i];
                handler.Enqueue(JsonResponse($"{{\"Title\":\"Product {i}\",\"Description\":\"Condition: Good\\nDetails: Description for product {i} that is definitely long enough\",\"CategoryId\":{cat}}}"));
            }

            var service = new AiImageService(new HttpClient(handler), _context);

            for (int i = 0; i < 15; i++)
            {
                var file = MakeFile($"p{i}.jpg", "image/jpeg", _tinyJpeg);
                var result = await service.GenerateListingFromImagesAsync(new[] { file }, CancellationToken.None);

                result.Should().NotBeNull($"image {i} should be recognized");
                result!.CategoryId.Should().Be(expectedCategories[i], $"image {i} should map to expected category");
                result.Title.Should().Be($"Product {i}");
            }
        }
    }
}
