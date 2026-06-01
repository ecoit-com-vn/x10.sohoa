using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using EvnHanoi.DigitizationService.Models;
using EvnHanoi.DigitizationService.Repositories;
using EvnHanoi.DigitizationService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EvnHanoi.DigitizationService.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/v1/folders")]
    public class VirtualFolderController : ControllerBase
    {
        private readonly IVirtualFolderRepository _folderRepository;
        private readonly IFileAttachmentRepository _fileRepository;
        private readonly IMinioStorageService _minioStorageService;
        private readonly ILogger<VirtualFolderController> _logger;
        private readonly string _bucketName;

        public VirtualFolderController(
            IVirtualFolderRepository folderRepository,
            IFileAttachmentRepository fileRepository,
            IMinioStorageService minioStorageService,
            IConfiguration configuration,
            ILogger<VirtualFolderController> logger)
        {
            _folderRepository = folderRepository;
            _fileRepository = fileRepository;
            _minioStorageService = minioStorageService;
            _logger = logger;
            _bucketName = configuration["Minio:BucketName"] ?? "digitization";
        }

        private long? GetUserUnitId()
        {
            var unitIdClaim = User.FindFirst("unit_id")?.Value;
            if (long.TryParse(unitIdClaim, out var unitId))
            {
                return unitId;
            }
            return null;
        }

        private string GetUsername()
        {
            return User.FindFirst(ClaimTypes.Name)?.Value ?? "System";
        }

        [HttpGet("tree")]
        public async Task<IActionResult> GetTree([FromQuery] string? equipmentId = null)
        {
            try
            {
                var unitId = GetUserUnitId();
                var allFolders = await _folderRepository.GetAllAsync(unitId, equipmentId);
                
                // Build tree
                var rootNodes = BuildFolderTree(allFolders);
                return Ok(rootNodes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting folder tree");
                return StatusCode(500, "Internal server error");
            }
        }

        private List<FolderTreeNode> BuildFolderTree(IEnumerable<VirtualFolder> folders)
        {
            var folderList = folders.ToList();
            var nodeMap = folderList.ToDictionary(f => f.Id, f => new FolderTreeNode
            {
                Id = f.Id,
                Name = f.Name,
                ParentId = f.ParentId,
                UnitId = f.UnitId,
                EquipmentId = f.EquipmentId,
                CreatedBy = f.CreatedBy,
                CreatedDate = f.CreatedDate,
                Children = new List<FolderTreeNode>()
            });

            var rootNodes = new List<FolderTreeNode>();

            foreach (var node in nodeMap.Values)
            {
                if (node.ParentId.HasValue && nodeMap.ContainsKey(node.ParentId.Value))
                {
                    nodeMap[node.ParentId.Value].Children.Add(node);
                }
                else
                {
                    rootNodes.Add(node);
                }
            }

            return rootNodes;
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(long id)
        {
            var folder = await _folderRepository.GetByIdAsync(id);
            if (folder == null) return NotFound();
            return Ok(folder);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] FolderCreateDto dto)
        {
            try
            {
                var username = GetUsername();
                var unitId = GetUserUnitId();

                var folder = new VirtualFolder
                {
                    Name = dto.Name,
                    ParentId = dto.ParentId,
                    UnitId = unitId,
                    EquipmentId = dto.EquipmentId,
                    CreatedBy = username,
                    CreatedDate = DateTime.UtcNow
                };

                var id = await _folderRepository.CreateAsync(folder);
                folder.Id = id;

                return CreatedAtAction(nameof(GetById), new { id = id }, folder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating folder");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(long id, [FromBody] FolderUpdateDto dto)
        {
            try
            {
                var folder = await _folderRepository.GetByIdAsync(id);
                if (folder == null) return NotFound();

                folder.Name = dto.Name;
                folder.ParentId = dto.ParentId;
                folder.EquipmentId = dto.EquipmentId;

                var success = await _folderRepository.UpdateAsync(folder);
                if (!success) return BadRequest("Failed to update folder.");

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating folder");
                return StatusCode(500, "Internal server error");
            }
        }


        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            try
            {
                var success = await _folderRepository.DeleteAsync(id);
                if (!success) return NotFound();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting folder");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{id}/files")]
        public async Task<IActionResult> GetFilesInFolder(long id)
        {
            try
            {
                var files = await _folderRepository.GetDocumentsInFolderAsync(id);
                return Ok(files);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting files in folder");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("{id}/files")]
        public async Task<IActionResult> UploadToFolder(long id, [FromForm] List<IFormFile> files)
        {
            if (files == null || files.Count == 0)
                return BadRequest("No files uploaded.");

            var folder = await _folderRepository.GetByIdAsync(id);
            if (folder == null) return NotFound("Folder not found.");

            var uploadedFiles = new List<object>();

            try
            {
                var username = GetUsername();

                foreach (var file in files)
                {
                    if (file.Length == 0) continue;

                    var objectName = $"{Guid.NewGuid()}_{file.FileName}";
                    
                    // 1. Upload to MinIO
                    using var stream = file.OpenReadStream();
                    var filePath = await _minioStorageService.UploadFileAsync(_bucketName, objectName, stream, file.ContentType);

                    // 2. Save to DB FILE_ATTACHMENT
                    var fileAttachment = new FileAttachment
                    {
                        FileName = file.FileName,
                        FilePath = filePath,
                        ContentType = file.ContentType,
                        FileSize = file.Length,
                        UploadedAt = DateTime.UtcNow,
                        UploadedBy = username,
                        Status = "Uploaded"
                    };

                    var fileId = await _fileRepository.CreateAsync(fileAttachment);

                    // 3. Link to Folder
                    await _folderRepository.AddDocumentToFolderAsync(id, fileId);

                    uploadedFiles.Add(new { FileId = fileId, FileName = file.FileName, FilePath = filePath });
                }

                return Ok(new { Message = "Files uploaded successfully", Files = uploadedFiles });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading files to folder");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpDelete("{id}/files/{fileId}")]
        public async Task<IActionResult> RemoveFileFromFolder(long id, int fileId)
        {
            try
            {
                await _folderRepository.RemoveDocumentFromFolderAsync(id, fileId);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing file from folder");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{id}/download-zip")]
        public async Task<IActionResult> DownloadFolderAsZip(long id)
        {
            var folder = await _folderRepository.GetByIdAsync(id);
            if (folder == null) return NotFound("Folder not found.");

            try
            {
                using var memoryStream = new MemoryStream();
                using (var zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
                {
                    await AddFolderToZipAsync(zipArchive, id, "");
                }

                memoryStream.Position = 0;
                var zipBytes = memoryStream.ToArray();

                var zipFileName = $"{folder.Name.Replace(" ", "_")}.zip";
                return File(zipBytes, "application/zip", zipFileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating zip download for folder {FolderId}", id);
                return StatusCode(500, "Internal server error during zip creation.");
            }
        }

        private async Task AddFolderToZipAsync(ZipArchive zipArchive, long folderId, string currentPath)
        {
            // 1. Get files in current folder and add to Zip
            var files = await _folderRepository.GetDocumentsInFolderAsync(folderId);
            foreach (var file in files)
            {
                try
                {
                    using var fileStream = await _minioStorageService.DownloadFileAsync(_bucketName, file.FilePath);
                    var entryPath = string.IsNullOrEmpty(currentPath) ? file.FileName : $"{currentPath}/{file.FileName}";
                    var zipEntry = zipArchive.CreateEntry(entryPath, CompressionLevel.Optimal);
                    using var entryStream = zipEntry.Open();
                    await fileStream.CopyToAsync(entryStream);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to add file {FileName} to zip archive", file.FileName);
                }
            }

            // 2. Get subfolders and recurse
            var subfolders = await _folderRepository.GetChildFoldersAsync(folderId);
            foreach (var subfolder in subfolders)
            {
                var nextPath = string.IsNullOrEmpty(currentPath) ? subfolder.Name : $"{currentPath}/{subfolder.Name}";
                await AddFolderToZipAsync(zipArchive, subfolder.Id, nextPath);
            }
        }
    }

    public class FolderTreeNode
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public long? ParentId { get; set; }
        public long? UnitId { get; set; }
        public string? EquipmentId { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public List<FolderTreeNode> Children { get; set; } = new List<FolderTreeNode>();
    }

    public class FolderCreateDto
    {
        public string Name { get; set; }
        public long? ParentId { get; set; }
        public string? EquipmentId { get; set; }
    }

    public class FolderUpdateDto
    {
        public string Name { get; set; }
        public long? ParentId { get; set; }
        public string? EquipmentId { get; set; }
    }
}
