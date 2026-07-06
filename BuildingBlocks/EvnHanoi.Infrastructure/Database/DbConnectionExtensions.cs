using System.Data;
using Oracle.ManagedDataAccess.Client;

namespace EvnHanoi.Infrastructure.Database;

public static class DbConnectionExtensions
{
    private static readonly HashSet<int> TransientOracleErrorCodes =
    [
        12570, // TNS:packet reader failure
        12571, // TNS:packet writer failure
        12170, // TNS:Connect timeout occurred
        3113,  // end-of-file on communication channel
        3114,  // not connected to ORACLE
        2396,  // exceeded maximum idle time
        28,    // session has been killed
    ];

    public static bool IsTransientOracle(this Exception exception)
    {
        for (var current = exception; current != null; current = current.InnerException)
        {
            if (current is OracleException oracleEx
                && TransientOracleErrorCodes.Contains(oracleEx.Number))
            {
                return true;
            }

            if (current is System.Net.Sockets.SocketException)
                return true;
        }

        return false;
    }

    public static void EnsureOpen(this IDbConnection connection, int maxAttempts = 3)
    {
        ArgumentNullException.ThrowIfNull(connection);

        Exception? lastError = null;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                if (connection.State == ConnectionState.Open)
                    return;

                connection.Open();
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts && ex.IsTransientOracle())
            {
                lastError = ex;
                ResetConnection(connection);
                Thread.Sleep(attempt * 300);
            }
        }

        throw lastError ?? new InvalidOperationException("Không thể mở kết nối Oracle.");
    }

    public static async Task<T> ExecuteWithRetryAsync<T>(
        this IDbConnection connection,
        Func<IDbConnection, Task<T>> operation,
        int maxAttempts = 3,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(operation);

        Exception? lastError = null;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                connection.EnsureOpen();
                return await operation(connection);
            }
            catch (Exception ex) when (attempt < maxAttempts && ex.IsTransientOracle())
            {
                lastError = ex;
                ResetConnection(connection);
                await Task.Delay(attempt * 300, cancellationToken);
            }
        }

        throw lastError ?? new InvalidOperationException("Thao tác Oracle thất bại sau khi retry.");
    }

    private static void ResetConnection(IDbConnection connection)
    {
        try
        {
            if (connection.State != ConnectionState.Closed)
                connection.Close();
        }
        catch
        {
            // Bỏ qua lỗi khi đóng connection đã hỏng.
        }

        if (connection is OracleConnection oracleConnection)
            OracleConnection.ClearPool(oracleConnection);
    }
}
