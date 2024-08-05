using Dapper;
using System.Data.SqlClient;
using System.Threading.Tasks;

public class JsonDataService
{
    private readonly string _connectionString;

    public JsonDataService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<string> GetJsonDataAsync(int id)
    {
        try
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                string query = "SELECT ParsedData FROM tblParsedCVData WHERE Id = @Id AND IsValidCV = 1";
                var jsonData = await connection.QuerySingleOrDefaultAsync<string>(query, new { Id = id });
                return jsonData;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Data Fetch Error: {ex.Message}");
            return null;
        }
    }
    public async Task<bool> UpdateIsStandardCVGeneratedAsync(int id)
    {
        try
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                string query = "UPDATE tblParsedCVData SET IsStandardCVGenerated = 1 WHERE Id = @Id";
                var rowsAffected = await connection.ExecuteAsync(query, new { Id = id });
                return rowsAffected > 0;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Update Error: {ex.Message}");
            return false;
        }
    }
}
