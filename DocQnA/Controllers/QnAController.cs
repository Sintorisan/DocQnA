using DocQnA.Services;
using Microsoft.AspNetCore.Mvc;

namespace DocQnA.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class QnAController : ControllerBase
    {
        private readonly BlobStorageService _blobStorageService;
        private readonly SearchService _searchService;

        public QnAController(BlobStorageService blobStorageService, SearchService searchService)
        {
            _blobStorageService = blobStorageService;
            _searchService = searchService;
        }

        [HttpPost("upload")]
        public async Task<ActionResult<string>> UploadDocument(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            var blobName = await _blobStorageService.UploadPdfAsync(file);
            if (string.IsNullOrEmpty(blobName))
                return BadRequest("Blob already exists or upload failed.");

            return Ok(blobName);
        }

        [HttpPost("prepare")]
        public async Task<ActionResult> PrepareDocument([FromBody] List<string> blobNames)
        {
            if (blobNames.Count == 0)
                return BadRequest("Invalid blob name.");

            await _blobStorageService.MoveDocumentsToQueryContainerAsync(blobNames);
            return NoContent();
        }

        [HttpPost("query")]
        public async Task<IActionResult> QueryDocument([FromBody] string question)
        {
            if (string.IsNullOrEmpty(question))
                return BadRequest("Invalid question.");

            var searchResults = await _searchService.SearchIndexAsync(question);
            return Ok(searchResults);
        }

        [HttpDelete("delete")]
        public async Task<IActionResult> DeleteDocument([FromQuery] string blobName)
        {
            if (string.IsNullOrEmpty(blobName))
                return BadRequest("Invalid blob name.");

            await _blobStorageService.DeletePdfAsync(blobName);
            return NoContent();
        }

        [HttpDelete("empty-container")]
        public async Task<IActionResult> EmptyQueryContainerAsync()
        {
            await _blobStorageService.EmptyQueryContainerAsync();
            return NoContent();
        }
    }
}