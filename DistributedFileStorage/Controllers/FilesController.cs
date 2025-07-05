using DistributedFileStorage.Services;
using Microsoft.AspNetCore.Mvc;

namespace DistributedFileStorage.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FilesController : ControllerBase
{
    private readonly ILogger<FilesController> _logger;
    private readonly string _nodeId;
    private readonly string _storagePath;
    private readonly IFileService _fileService;

    public FilesController(ILogger<FilesController> logger, IConfiguration config, IFileService fileService)
    {
        _logger = logger;
        _nodeId = config.GetValue<string>("NodeId")!;
        _storagePath = config.GetValue<string>("StoragePath")!;
        _fileService = fileService;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> Upload(IFormFile file, [FromQuery] string? path)
    {
        if (file == null || file.Length == 0)
            return BadRequest("File not provided");

        var meta = await _fileService.UploadFileAsync(file, path, _nodeId);

        return Ok(new
        {
            meta.Id,
            meta.FilePath,
            meta.Size,
            meta.Revision
        });
    }

    [HttpGet("{**filePath}")]
    public async Task<IActionResult> Download(string filePath, [FromQuery] int? revision)
    {
        var meta = await _fileService.GetFileMetadataAsync(filePath, revision);
        if (meta == null) return NotFound();

        var stream = _fileService.GetFileStream(meta, _storagePath);
        if (stream == null) return NotFound();

        Response.Headers["Content-Disposition"] = $"inline; filename=\"{Path.GetFileName(meta.FilePath)}\"";
        return File(stream, meta.ContentType ?? "application/octet-stream");
    }

    [HttpDelete("{**filePath}")]
    public async Task<IActionResult> Delete(string filePath, [FromQuery] int? revision)
    {
        var deleted = await _fileService.DeleteFileAsync(filePath, revision, _nodeId);

        if (!deleted.Any()) return NotFound("No files found to delete.");

        return Ok(new
        {
            Message = $"Deleted {deleted.Count} revision(s) of {filePath}",
            Deleted = deleted.Select(d => new { d.FileName, d.Revision })
        });
    }
}
