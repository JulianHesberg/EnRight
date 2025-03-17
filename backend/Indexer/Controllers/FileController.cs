using Indexer.Models;
using Indexer.Services;
using Microsoft.AspNetCore.Mvc;

namespace Indexer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FileController : ControllerBase
{
    private readonly FileService _fileService;

    public FileController(FileService service)
    {
        _fileService = service;
    }

    [HttpGet]
    [Route("search")]
    public async Task<List<FileSearchResult>> SearchFilesAsync(string searchQuery)
    {
        try
        {
            return await _fileService.GetTop20Files(searchQuery);
        }
        catch (Exception ex)
        {
            throw ex;
        }
    }
}
