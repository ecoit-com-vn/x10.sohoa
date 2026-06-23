using Microsoft.Extensions.Configuration;
using System.Net.Sockets;
using System.Text;

namespace EvnHanoi.EquipmentService.Core.Services;

/// <summary>
/// Service cho quét antivirus bằng ClamAV
/// </summary>
public interface IClamAvService
{
    /// <summary>
    /// Scan file từ stream
    /// </summary>
    /// <param name="fileStream">Stream của file cần scan</param>
    /// <param name="fileName">Tên file (cho log)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>ScanResult với IsClean flag</returns>
    Task<ScanResult> ScanFileAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default);
}

public record ScanResult
{
    public bool IsClean { get; set; }
    public string? Threat { get; set; }  // Tên threat nếu detect
    public string? Report { get; set; }  // Chi tiết report
}

public class ClamAvService : IClamAvService
{
    private readonly IConfiguration _config;
    private readonly ILogger<ClamAvService> _logger;
    private readonly string _host;
    private readonly int _port;
    private readonly int _timeoutSeconds;

    public ClamAvService(IConfiguration config, ILogger<ClamAvService> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _host = _config["Antivirus:ClamAvHost"] ?? "clamav-service";
        _port = int.TryParse(_config["Antivirus:ClamAvPort"], out var port) ? port : 3310;
        _timeoutSeconds = int.TryParse(_config["Antivirus:ScanTimeoutSeconds"], out var timeout) ? timeout : 300;
    }

    public async Task<ScanResult> ScanFileAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if antivirus is enabled
            var enabled = _config.GetValue<bool>("Antivirus:ClamAvEnabled");
            if (!enabled)
            {
                _logger.LogInformation("Antivirus disabled - scanning skipped for {FileName}", fileName);
                return new ScanResult { IsClean = true };
            }

            // Create TCP connection
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(_host, _port);
            
            // Apply timeout
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(_timeoutSeconds));

            try
            {
                await connectTask.WaitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogError("ClamAV scan timeout for {FileName} - exceeded {Timeout}s", fileName, _timeoutSeconds);
                return new ScanResult 
                { 
                    IsClean = false, 
                    Threat = "TIMEOUT",
                    Report = $"Scan timeout after {_timeoutSeconds} seconds"
                };
            }

            using var stream = client.GetStream();

            // INSTREAM command for ClamAV scanning
            var cmd = Encoding.ASCII.GetBytes("INSTREAM\r\n");
            await stream.WriteAsync(cmd, 0, cmd.Length, cts.Token);

            // Send file chunks
            byte[] buffer = new byte[4096];
            int bytesRead;
            long totalSent = 0;

            while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                // ClamAV INSTREAM format: send 4-byte length + data for each chunk
                byte[] length = BitConverter.GetBytes(bytesRead);
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(length);

                await stream.WriteAsync(length, 0, 4, cts.Token);
                await stream.WriteAsync(buffer, 0, bytesRead, cts.Token);
                totalSent += bytesRead;
            }

            // Send empty chunk to signal end
            await stream.WriteAsync(new byte[] { 0, 0, 0, 0 }, 0, 4, cts.Token);

            // Read response
            var responseBuffer = new byte[1024];
            var responseLength = await stream.ReadAsync(responseBuffer, 0, responseBuffer.Length, cts.Token);
            var response = Encoding.ASCII.GetString(responseBuffer, 0, responseLength).Trim();

            _logger.LogInformation("ClamAV response for {FileName} ({Bytes} bytes): {Response}", fileName, totalSent, response);

            // Parse response
            // Format: "stream: OK" or "stream: EICAR-STANDARD-AV-TEST-FILE!Eicar-Test-File FOUND"
            if (response.Contains("OK"))
            {
                return new ScanResult { IsClean = true };
            }
            else if (response.Contains("FOUND"))
            {
                var threat = response.Split(':')[1].Trim();
                return new ScanResult 
                { 
                    IsClean = false, 
                    Threat = threat,
                    Report = response
                };
            }
            else
            {
                _logger.LogWarning("Unknown ClamAV response for {FileName}: {Response}", fileName, response);
                return new ScanResult { IsClean = false, Threat = "UNKNOWN_RESPONSE", Report = response };
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("ClamAV scan cancelled for {FileName}", fileName);
            return new ScanResult 
            { 
                IsClean = false, 
                Threat = "CANCELLED",
                Report = "Scan was cancelled"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ClamAV scan error for {FileName}", fileName);
            return new ScanResult 
            { 
                IsClean = false, 
                Threat = "ERROR",
                Report = ex.Message
            };
        }
    }
}
