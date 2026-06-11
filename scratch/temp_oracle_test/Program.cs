using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;
using Dapper;

class Program
{
    static async Task Main()
    {
        string host = "192.168.1.199";
        int port = 1521;
        string user = "c##qlshx10";
        string password = "Ecoit@123qwe";
        string service = "orcl";
        
        string connStr = $"Data Source={host}:{port}/{service};User Id={user};Password={password};Pooling=false;";
        try
        {
            Console.WriteLine("Connecting to Oracle database...");
            using (var conn = new OracleConnection(connStr))
            {
                conn.Open();
                Console.WriteLine("Connected!");

                // Testing INSERT with :Action and :Comment
                Console.WriteLine("\n--- Running INSERT with :Action and :Comment ---");
                try
                {
                    var sql = @"INSERT INTO WORKFLOWHISTORY (
                                    Id, 
                                    WorkflowInstanceId, 
                                    StepName, 
                                    ""ACTION"", 
                                    ActionByUserId, 
                                    ""Comment"", 
                                    ActionDate
                                )
                                VALUES (:Id, :WorkflowInstanceId, :StepName, :Action, :ActionByUserId, :Comment, :ActionDate)";
                    
                    var parameters = new DynamicParameters();
                    parameters.Add("Id", Guid.NewGuid().ToString());
                    parameters.Add("WorkflowInstanceId", Guid.NewGuid().ToString());
                    parameters.Add("StepName", "Test Step");
                    parameters.Add("Action", "Submit");
                    parameters.Add("ActionByUserId", "admin");
                    parameters.Add("Comment", "Test comment");
                    parameters.Add("ActionDate", DateTime.UtcNow);

                    var affected = await conn.ExecuteAsync(sql, parameters);
                    Console.WriteLine($"INSERT with :Action and :Comment Success! Affected: {affected}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"INSERT with :Action and :Comment Error: {ex.Message}");
                }

                // Testing INSERT with safe parameter names
                Console.WriteLine("\n--- Running INSERT with :ActionVal and :CommentVal ---");
                try
                {
                    var sql = @"INSERT INTO WORKFLOWHISTORY (
                                    Id, 
                                    WorkflowInstanceId, 
                                    StepName, 
                                    ""ACTION"", 
                                    ActionByUserId, 
                                    ""Comment"", 
                                    ActionDate
                                )
                                VALUES (:Id, :WorkflowInstanceId, :StepName, :ActionVal, :ActionByUserId, :CommentVal, :ActionDate)";
                    
                    var parameters = new DynamicParameters();
                    parameters.Add("Id", Guid.NewGuid().ToString());
                    parameters.Add("WorkflowInstanceId", Guid.NewGuid().ToString());
                    parameters.Add("StepName", "Test Step");
                    parameters.Add("ActionVal", "Submit");
                    parameters.Add("ActionByUserId", "admin");
                    parameters.Add("CommentVal", "Test comment");
                    parameters.Add("ActionDate", DateTime.UtcNow);

                    var affected = await conn.ExecuteAsync(sql, parameters);
                    Console.WriteLine($"INSERT with :ActionVal and :CommentVal Success! Affected: {affected}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"INSERT with :ActionVal and :CommentVal Error: {ex.Message}");
                }



                // Check table columns of WORKFLOWHISTORY
                Console.WriteLine("\n--- Checking columns of WORKFLOWHISTORY ---");
                try
                {
                    var cols = await conn.QueryAsync<string>(
                        "SELECT column_name FROM user_tab_cols WHERE table_name = 'WORKFLOWHISTORY'");
                    Console.WriteLine("Columns in WORKFLOWHISTORY table:");
                    Console.WriteLine(string.Join(", ", cols));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error querying WORKFLOWHISTORY columns: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}

