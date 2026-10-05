// Harness xác minh LUỒNG UPLOAD THẬT: dùng FileUploadService + DocumentDigitizationService (code thật) +
// DocumentCompressionService thật; chỉ thay MinIO/Oracle bằng bản giả trong bộ nhớ.
using System.Reflection;
using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;
using EvnHanoi.EquipmentService.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

var dir = args.Length > 0 ? args[0] : ".";
byte[] Load(string n) => File.ReadAllBytes(Path.Combine(dir, n));

var failures = new List<string>();
void Check(string label, bool ok, string detail = "") { Console.WriteLine($"[{(ok ? "OK" : "FAIL")}] {label} {(detail == "" ? "" : "— " + detail)}"); if (!ok) failures.Add(label); }
static bool IsPdf(byte[] b) => b != null && b.Length > 4 && b[0] == 0x25 && b[1] == 0x50 && b[2] == 0x44 && b[3] == 0x46;
static bool IsJpeg(byte[] b) => b != null && b.Length > 3 && b[0] == 0xFF && b[1] == 0xD8;

var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
{
    ["FileUpload:ChunkSizeBytes"] = "40000",
    ["FileUpload:MaxFileSizeBytes"] = "52428800",
    ["FileUpload:UploadSessionExpiryMinutes"] = "60",
}).Build();

var cfgOff = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
{
    ["FileUpload:ChunkSizeBytes"] = "40000", ["FileUpload:MaxFileSizeBytes"] = "52428800", ["FileUpload:UploadSessionExpiryMinutes"] = "60",
    ["DocumentProcessing:ConvertImageToPdf:Enabled"] = "false",
}).Build();

Env Build(IConfiguration c)
{
    var e = new Env();
    e.Repo = DispatchProxy.Create<IDocumentRepository, FakeProxy>();
    ((FakeProxy)(object)e.Repo).State = e;
    e.Storage = new FakeStorage(e);
    var comp = new EvnHanoi.DocumentProcessing.DocumentCompressionService(NullLogger<EvnHanoi.DocumentProcessing.DocumentCompressionService>.Instance);
    e.Comp = comp;
    e.Upload = new FileUploadService(e.Repo, e.Storage, new FakeClam(), new MimeTypeValidationService(e.Repo, NullLogger<MimeTypeValidationService>.Instance), comp, c, NullLogger<FileUploadService>.Instance);
    return e;
}

var dossierId = Guid.NewGuid();
var docTypeId = Guid.NewGuid();
var folderId = Guid.NewGuid();
const long unit = 1;

// ============ A. Dossier DIRECT upload (E3) ============
{
    var e = Build(cfg);
    using var ms = new MemoryStream(Load("t_normal.jpg"));
    var r = await e.Upload.UploadFileToDossierDirectAsync(ms, "scan_bien_ban.jpg", "image/jpeg", ms.Length, dossierId, docTypeId, 3, "u1", unit, "Tester", default);
    var v = e.Versions.Single(); var d = e.Documents.Single();
    var bytes = e.Objects[(e.Storage.DossierBucketName, v.FilePath)];
    Check("A. Dossier direct JPEG → Document.Name .pdf", d.Name == "scan_bien_ban.pdf", d.Name);
    Check("A. MIME=application/pdf, key .pdf, bytes là PDF thật", v.MimeType == "application/pdf" && v.FilePath.EndsWith(".pdf") && IsPdf(bytes), $"{v.MimeType} {v.FilePath}");
    Check("A. PageCount = 1", v.PageCount == 1, v.PageCount.ToString());
}
{
    var e = Build(cfg);
    using var ms = new MemoryStream(Load("t_multi.tif"));
    await e.Upload.UploadFileToDossierDirectAsync(ms, "phu_luc.tiff", "image/tiff", ms.Length, dossierId, docTypeId, 3, "u1", unit, "Tester", default);
    var v = e.Versions.Single();
    Check("A. Dossier direct TIFF 2 trang → PDF 2 trang", v.PageCount == 2 && v.MimeType == "application/pdf", $"pages={v.PageCount}");
}
{
    var e = Build(cfg);
    using var ms = new MemoryStream(Load("t_corrupt.jpg"));
    Exception ex = null;
    try { await e.Upload.UploadFileToDossierDirectAsync(ms, "hong.jpg", "image/jpeg", ms.Length, dossierId, docTypeId, 3, "u1", unit, "Tester", default); } catch (Exception x) { ex = x; }
    Check("A. Ảnh hỏng → bị từ chối (InvalidOperationException), không tạo document/object", ex is InvalidOperationException && e.Documents.Count == 0 && e.Objects.Count == 0, ex?.GetType().Name + ": " + ex?.Message);
}
{
    var e = Build(cfg);
    using var ms = new MemoryStream(Load("t_normal.pdf"));
    await e.Upload.UploadFileToDossierDirectAsync(ms, "ban_dien_tu.pdf", "application/pdf", ms.Length, dossierId, docTypeId, 3, "u1", unit, "Tester", default);
    var v = e.Versions.Single();
    Check("A. Hồi quy: PDF vẫn upload bình thường", v.MimeType == "application/pdf" && e.Documents.Single().Name == "ban_dien_tu.pdf");
}
{
    var e = Build(cfgOff);
    using var ms = new MemoryStream(Load("t_normal.jpg"));
    await e.Upload.UploadFileToDossierDirectAsync(ms, "x.jpg", "image/jpeg", ms.Length, dossierId, docTypeId, 3, "u1", unit, "Tester", default);
    var v = e.Versions.Single();
    Check("A. Cờ tắt → ảnh giữ nguyên (hành vi cũ)", v.MimeType == "image/jpeg" && e.Documents.Single().Name == "x.jpg" && IsJpeg(e.Objects[(e.Storage.DossierBucketName, v.FilePath)]));
}

// ============ B. Dossier CHUNKED (E4) ============
async Task<FileUploadResponse> Chunked(Env e, byte[] data, string fileName, bool expectFail = false)
{
    var init = await e.Upload.InitiateDossierChunkedUploadAsync(fileName, data.Length, dossierId, "u1", default);
    e.LastInit = init;
    var parts = new List<UploadChunkRequest>();
    for (int i = 0; i < init.TotalChunks; i++)
    {
        var slice = data.Skip(i * init.ChunkSize).Take(init.ChunkSize).ToArray();
        using var cs = new MemoryStream(slice);
        var etag = await e.Upload.UploadDossierChunkAsync(init.UploadId, dossierId, i + 1, cs, slice.Length, unit, default);
        parts.Add(new UploadChunkRequest { ChunkNumber = i + 1, ETag = etag });
    }
    return await e.Upload.CompleteDossierChunkedUploadAsync(init.UploadId, dossierId, new CompleteChunkedUploadRequest { UploadId = init.UploadId, Parts = parts, DocumentTypeId = docTypeId }, "u1", unit, "Tester", default);
}
{
    var e = Build(cfg);
    var data = Load("t_normal.jpg");
    var r = await Chunked(e, data, "chup_dien_thoai.jpg");
    var v = e.Versions.Single(); var d = e.Documents.Single();
    Check("B. Initiate trả fileName .pdf", e.LastInit.FileName == "chup_dien_thoai.pdf", e.LastInit.FileName);
    Check($"B. Chunked ({e.LastInit.TotalChunks} chunk) → Name .pdf, MIME pdf, key .pdf", d.Name == "chup_dien_thoai.pdf" && v.MimeType == "application/pdf" && v.FilePath.EndsWith(".pdf"), $"{d.Name} {v.MimeType}");
    Check("B. Object trên kho là PDF thật (đã ghi đè bytes ảnh)", IsPdf(e.Objects[(e.Storage.DossierBucketName, v.FilePath)]));
    Check("B. PageCount=1", v.PageCount == 1, v.PageCount.ToString());
}
{
    var e = Build(cfg);
    var r = await Chunked(e, Load("t_multi.tif"), "ho_so.tif");
    var v = e.Versions.Single();
    Check("B. Chunked TIFF 2 trang → PDF 2 trang", v.PageCount == 2 && v.MimeType == "application/pdf", $"pages={v.PageCount}");
}
{
    var e = Build(cfg);
    var data = Load("t_corrupt.jpg").Concat(new byte[100]).ToArray();
    Exception ex = null;
    try { await Chunked(e, data, "hong.jpg"); } catch (Exception x) { ex = x; }
    var sess = e.Sessions.Values.Single();
    Check("B. Ảnh hỏng (chunked) → ném lỗi, session Failed, KHÔNG còn object mồ côi, không tạo document",
        ex is InvalidOperationException && sess.Status == "Failed" && e.Objects.Count == 0 && e.Documents.Count == 0,
        $"{ex?.GetType().Name} status={sess.Status} objects={e.Objects.Count}");
}
{
    var e = Build(cfg);
    var r = await Chunked(e, Load("t_normal.pdf"), "bao_cao.pdf");
    var v = e.Versions.Single();
    Check("B. Hồi quy: PDF chunked bình thường", v.MimeType == "application/pdf" && e.Documents.Single().Name == "bao_cao.pdf");
}

// ============ C. New version chunked (E5): hồ sơ → chuyển; thư mục Kho TL → giữ ảnh ============
async Task<FileUploadResponse> NewVersion(Env e, Guid docId, byte[] data, string fileName)
{
    var init = await e.Upload.InitiateNewVersionChunkedUploadAsync(docId, fileName, data.Length, "u1", unit, default);
    e.LastInit = init;
    var parts = new List<UploadChunkRequest>();
    for (int i = 0; i < init.TotalChunks; i++)
    {
        var slice = data.Skip(i * init.ChunkSize).Take(init.ChunkSize).ToArray();
        using var cs = new MemoryStream(slice);
        // Phiên bản mới của tài liệu HỒ SƠ: UploadChunkAsync (endpoint thật của DocumentController) đòi session.FolderId nên
        // không dùng được → dùng UploadDossierChunkAsync để vẫn thực thi đoạn Complete cần kiểm chứng.
        var etag = e.ExistingDocs[docId].DossierId.HasValue
            ? await e.Upload.UploadDossierChunkAsync(init.UploadId, e.ExistingDocs[docId].DossierId.Value, i + 1, cs, slice.Length, unit, default)
            : await e.Upload.UploadChunkAsync(init.UploadId, i + 1, cs, slice.Length, default);
        parts.Add(new UploadChunkRequest { ChunkNumber = i + 1, ETag = etag });
    }
    return await e.Upload.CompleteNewVersionChunkedUploadAsync(docId, init.UploadId, new CompleteChunkedUploadRequest { UploadId = init.UploadId, Parts = parts }, "u1", unit, default);
}
{
    var e = Build(cfg);
    var docId = Guid.NewGuid();
    e.ExistingDocs[docId] = new DocumentListItemDto { Id = docId, Name = "cu.pdf", DossierId = dossierId, FolderId = null };
    var r = await NewVersion(e, docId, Load("t_exif6.jpg"), "phien_ban_moi.jpg");
    var v = e.Versions.Single();
    Check("C. Phiên bản mới (hồ sơ) ảnh → PDF", e.LastInit.FileName == "phien_ban_moi.pdf" && v.MimeType == "application/pdf" && IsPdf(e.Objects[(e.Storage.DossierBucketName, v.FilePath)]), $"{e.LastInit.FileName} {v.MimeType}");
}
{
    var e = Build(cfg);
    var docId = Guid.NewGuid();
    e.ExistingDocs[docId] = new DocumentListItemDto { Id = docId, Name = "anh_thiet_bi.jpg", FolderId = folderId, DossierId = null };
    var r = await NewVersion(e, docId, Load("t_normal.jpg"), "anh_moi.jpg");
    var v = e.Versions.Single();
    Check("C. Phiên bản mới (Kho TL) ảnh → GIỮ NGUYÊN ảnh (D1)", e.LastInit.FileName == "anh_moi.jpg" && v.MimeType == "image/jpeg" && IsJpeg(e.Objects[(e.Storage.DocumentBucketName, v.FilePath)]), $"{e.LastInit.FileName} {v.MimeType}");
}

// ============ D. Kho TL upload trực tiếp (E1) giữ ảnh ============
{
    var e = Build(cfg);
    using var ms = new MemoryStream(Load("t_normal.jpg"));
    await e.Upload.UploadFileDirectAsync(ms, "anh_tram.jpg", "image/jpeg", ms.Length, folderId, 1, "u1", unit, default);
    var v = e.Versions.Single();
    Check("D. Kho TL direct: ảnh KHÔNG bị chuyển PDF (D1)", e.Documents.Single().Name == "anh_tram.jpg" && v.MimeType.StartsWith("image/"), $"{e.Documents.Single().Name} {v.MimeType}");
}

// ============ E. Ảnh cũ (D5): chuyển khi bấm OCR ============
{
    var e = Build(cfg);
    var docId = Guid.NewGuid(); var verId = Guid.NewGuid();
    const string key = "unit/dossiers/legacy/abc_anh_cu.jpg";
    var jpg = Load("t_exif6.jpg");
    e.Objects[(e.Storage.DossierBucketName, key)] = jpg;
    e.ExistingDocs[docId] = new DocumentListItemDto { Id = docId, Name = "anh_cu.jpg", DossierId = dossierId };
    var version = new DocumentVersionDto { Id = verId, DocumentId = docId, FilePath = key, MimeType = "image/jpeg", FileSize = jpg.Length, MinioVersionId = "v0" };
    var svc = new DocumentDigitizationService(null, e.Repo, null, null, null, e.Storage, null, null, null, null, e.Comp, cfg, NullLogger<DocumentDigitizationService>.Instance);
    var m = typeof(DocumentDigitizationService).GetMethod("EnsureOcrReadyAsync", BindingFlags.NonPublic | BindingFlags.Instance);
    await (Task)m.Invoke(svc, new object[] { version, e.Storage.DossierBucketName, "u1" });
    var after = e.Objects[(e.Storage.DossierBucketName, key)];
    Check("E. Ảnh cũ: object tại key cũ giờ là PDF", IsPdf(after));
    Check("E. DB: MIME=pdf, PageCount=1, size cập nhật, tên .pdf", e.ContentUpdates.Count == 1 && e.ContentUpdates[0].mime == "application/pdf" && e.ContentUpdates[0].pages == 1 && e.ContentUpdates[0].size == after.Length && e.NameUpdates.SingleOrDefault().name == "anh_cu.pdf",
        $"updates={e.ContentUpdates.Count} name={e.NameUpdates.FirstOrDefault().name}");
    Check("E. DTO phiên bản được cập nhật mime=pdf", version.MimeType == "application/pdf");
    // gọi lần 2 (đã là PDF theo DTO) → no-op
    var before = e.ContentUpdates.Count;
    await (Task)m.Invoke(svc, new object[] { version, e.Storage.DossierBucketName, "u1" });
    Check("E. Gọi lại khi đã là PDF → no-op", e.ContentUpdates.Count == before);
    // đua: DTO nói image nhưng bytes đã PDF
    var raced = new DocumentVersionDto { Id = verId, DocumentId = docId, FilePath = key, MimeType = "image/jpeg", FileSize = 1, MinioVersionId = "v1" };
    var pdfBefore = e.Objects[(e.Storage.DossierBucketName, key)];
    await (Task)m.Invoke(svc, new object[] { raced, e.Storage.DossierBucketName, "u1" });
    Check("E. Đua (DB nói ảnh, bytes đã PDF) → không ghi đè object, chỉ sửa MIME", ReferenceEquals(pdfBefore, e.Objects[(e.Storage.DossierBucketName, key)]) && raced.MimeType == "application/pdf");
}

Console.WriteLine();
Console.WriteLine(failures.Count == 0 ? "=== TẤT CẢ PASS ===" : $"=== {failures.Count} FAIL: {string.Join("; ", failures)} ===");
return failures.Count == 0 ? 0 : 1;

// ---------------- fakes ----------------
class Env
{
    public IDocumentRepository Repo; public FakeStorage Storage; public FileUploadService Upload;
    public EvnHanoi.DocumentProcessing.DocumentCompressionService Comp;
    public Dictionary<(string, string), byte[]> Objects = new();
    public Dictionary<string, List<byte[]>> Chunks = new();
    public List<Document> Documents = new(); public List<DocumentVersion> Versions = new();
    public Dictionary<string, UploadSession> Sessions = new();
    public Dictionary<Guid, DocumentListItemDto> ExistingDocs = new();
    public List<(Guid id, string mime, int pages, long size)> ContentUpdates = new();
    public List<(Guid id, string name)> NameUpdates = new();
    public InitiateChunkedUploadResponse LastInit;
}
class FakeClam : IClamAvService { public Task<ScanResult> ScanFileAsync(Stream s, string f, CancellationToken ct = default) => Task.FromResult(new ScanResult { IsClean = true }); }

class FakeProxy : DispatchProxy
{
    public Env State;
    protected override object Invoke(MethodInfo m, object[] a)
    {
        object result = m.Name switch
        {
            "GetOrganizationUnitCodeAsync" => "UNIT1",
            "GetFolderByIdAsync" => new FolderNodeDto { Id = (Guid)a[0], UnitId = 1, UnitCode = "UNIT1" },
            "CreateDocumentAsync" => Do(() => { var d = (Document)a[0]; d.Id = Guid.NewGuid(); State.Documents.Add(d); return d.Id; }),
            "CreateDocumentVersionAsync" => Do(() => { var v = (DocumentVersion)a[0]; v.Id = Guid.NewGuid(); State.Versions.Add(v); return v.Id; }),
            "CreateUploadSessionAsync" => Do(() => { var s = (UploadSession)a[0]; s.Id = Guid.NewGuid(); State.Sessions[s.UploadId] = s; return s.Id; }),
            "GetUploadSessionAsync" => State.Sessions.TryGetValue((string)a[0], out var ss) ? ss : null,
            "UpdateUploadSessionAsync" => true,
            "CompleteUploadSessionAsync" => Do(() => { State.Sessions[(string)a[0]].Status = "Completed"; return true; }),
            "GetDocumentByIdAsync" => State.ExistingDocs.TryGetValue((Guid)a[0], out var dd) ? dd : null,
            "GetDocumentVersionsAsync" => new List<DocumentVersionDto>(),
            "GetMaxDocumentVersionNumberAsync" => 0,
            "UpdateDocumentVersionContentAsync" => Do(() => { State.ContentUpdates.Add(((Guid)a[0], (string)a[3], (int)a[4], (long)a[2])); return true; }),
            "UpdateDocumentNameAsync" => Do(() => { State.NameUpdates.Add(((Guid)a[0], (string)a[1])); return true; }),
            _ => null
        };
        var rt = m.ReturnType;
        if (rt == typeof(Task)) return Task.CompletedTask;
        if (rt.IsGenericType && rt.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var inner = rt.GetGenericArguments()[0];
            object val = result;
            if (val == null && inner.IsValueType) val = Activator.CreateInstance(inner);
            return typeof(Task).GetMethod(nameof(Task.FromResult)).MakeGenericMethod(inner).Invoke(null, new[] { val });
        }
        return result;
    }
    static object Do(Func<object> f) => f();
}

class FakeStorage : IFileStorageService
{
    readonly Env e; public FakeStorage(Env env) { e = env; }
    public string DocumentBucketName => "documents";
    public string DossierBucketName => "dossiers";
    public string BuildDossierObjectKey(string unitCode, Guid dossierId, string fileName) => $"{unitCode}/dossiers/{dossierId}/{Guid.NewGuid():N}_{fileName}";
    string FolderKey(string unit, Guid f, string n) => $"{unit}/folders/{f}/{Guid.NewGuid():N}_{n}";
    public Task<(string, string)> UploadFileAsync(Stream s, string n, string m, long sz, string unit, Guid f, CancellationToken ct = default)
    { var k = FolderKey(unit, f, n); e.Objects[(DocumentBucketName, k)] = Read(s); return Task.FromResult((k, "v1")); }
    public Task<(string, string)> UploadFileToDossierAsync(Stream s, string n, string m, long sz, string unit, Guid d, CancellationToken ct = default)
    { var k = BuildDossierObjectKey(unit, d, n); e.Objects[(DossierBucketName, k)] = Read(s); return Task.FromResult((k, "v1")); }
    public Task<string> UploadChunkAsync(string up, int n, Stream c, long sz, string unit, CancellationToken ct = default)
    { if (!e.Chunks.ContainsKey(up)) e.Chunks[up] = new(); e.Chunks[up].Add(Read(c)); return Task.FromResult($"etag{n}"); }
    public Task<(string, long, string)> MergeChunksAsync(string up, int total, string unit, Guid f, string n, CancellationToken ct = default)
    { var all = e.Chunks[up].SelectMany(x => x).ToArray(); var k = FolderKey(unit, f, n); e.Objects[(DocumentBucketName, k)] = all; return Task.FromResult((k, (long)all.Length, "v1")); }
    public Task<(string, long, string)> MergeChunksToDossierAsync(string up, int total, string unit, Guid d, string n, CancellationToken ct = default)
    { var all = e.Chunks[up].SelectMany(x => x).ToArray(); var k = BuildDossierObjectKey(unit, d, n); e.Objects[(DossierBucketName, k)] = all; return Task.FromResult((k, (long)all.Length, "v1")); }
    public Task<Stream> DownloadFileAsync(string p, string b = null, string v = null, CancellationToken ct = default)
        => Task.FromResult<Stream>(new MemoryStream(e.Objects[(b ?? DocumentBucketName, p)]));
    public Task<bool> DeleteFileAsync(string p, string b = null, string v = null, CancellationToken ct = default)
    { e.Objects.Remove((b ?? DocumentBucketName, p)); return Task.FromResult(true); }
    public Task<bool> DeleteUploadSessionAsync(string up, int t, string u, CancellationToken ct = default) => Task.FromResult(true);
    public Task<bool> AbortUploadSessionAsync(string up, string u, CancellationToken ct = default) => Task.FromResult(true);
    public Task<(long, string)> ReplaceObjectAsync(string k, string b, Stream s, long sz, string m, CancellationToken ct = default)
    { var bytes = Read(s); e.Objects[(b, k)] = bytes; return Task.FromResult(((long)bytes.Length, "v2")); }
    public Task<string> CopyFileAsync(string a, string b, string sb = null, string db = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<(string, string)> CopyFileWithVersionAsync(string a, string b, string sb = null, string db = null, string sv = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<(string, string)> UploadPmisDocumentAsync(Stream s, string n, string m, long sz, string ot, Guid oid, CancellationToken ct = default) => throw new NotImplementedException();
    public string BuildPmisDocumentObjectKey(string ot, Guid oid, string n) => throw new NotImplementedException();
    static byte[] Read(Stream s) { using var ms = new MemoryStream(); s.CopyTo(ms); return ms.ToArray(); }
}
