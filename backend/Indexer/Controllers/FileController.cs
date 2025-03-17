using System.Diagnostics;
using System.Diagnostics.Metrics;
using Indexer.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Indexer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FileController : ControllerBase
    {
        private readonly FileService _fileService;
        private readonly ILogger<FileController> _logger;

        private static readonly ActivitySource ActivitySource = new("Indexer.FileController");

        private static readonly Meter s_meter = new("IndexerMeter");
        private static readonly Counter<long> s_searchRequests =
            s_meter.CreateCounter<long>("search_requests", "Number of search requests received by FileController");

        public FileController(FileService service, ILogger<FileController> logger)
        {
            _fileService = service;
            _logger = logger;
        }

        [HttpGet("search")]
        public async Task <IActionResult> SearchFilesAsync(string searchQuery)
        {
            using var activity = ActivitySource.StartActivity("SearchFiles", ActivityKind.Server);

            s_searchRequests.Add(1, new KeyValuePair<string, object?>("query", searchQuery ?? ""));

            _logger.LogInformation("Handling search request for {Query}", searchQuery);

            try
            {
                var results = await _fileService.GetTop20Files(searchQuery);

                _logger.LogInformation("Returning {Count} results for {Query}", results.Count, searchQuery);
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching for {Query}", searchQuery);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }
    }
}
