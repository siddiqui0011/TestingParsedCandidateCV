using Dapper;
using Microsoft.AspNetCore.Mvc;
using System.Data.SqlClient;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace TestingParsedCandidateCV.Controllers
{
    [Route("api/[controller]")]                     
                                                    
    [ApiController]
    public class PdfController : ControllerBase
    {
        private readonly string _connectionString;
        private readonly JsonDataService _jsonDataService;
        private readonly JsonParser _jsonParser;
        public PdfController(JsonDataService jsonDataService, JsonParser jsonParser, IConfiguration configuration)
        {
            _jsonDataService = jsonDataService;
            _jsonParser = jsonParser;
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        [HttpGet("generatePdf/{id}")]
        public async Task<IActionResult> GeneratePdf(int id)
        {
            var jsonData = await _jsonDataService.GetJsonDataAsync(id);
            if (jsonData == null)
            {
                return NotFound($"No valid CV data found for ID {id}");
            }

            var candidate = _jsonParser.ParseJsonData(jsonData);
            var pdfPath = PdfGenerator.GeneratePdf(candidate);

            var pdfBytes = await System.IO.File.ReadAllBytesAsync(pdfPath);
            System.IO.File.Delete(pdfPath); // Clean up file after serving
            if (!jsonData.Any())
            {
                return NotFound("No valid CV data found for the provided IDs");
            }

            return File(pdfBytes, "application/pdf", pdfPath);

        }

        [HttpPost("generatePdfByIds")]
        public async Task<IActionResult> GeneratePdfByIds([FromBody] List<int> ids)
        {
            var pdfPaths = new List<string>();

            var idsToProcess = new List<int>();

            //// Fetch IDs of records that are not yet processed
            using (var connection = new SqlConnection(_connectionString))
            {
                string query = "SELECT Id FROM tblParsedCVData WHERE Id IN @Ids AND IsStandardCVGenerated = 0";
                idsToProcess = (await connection.QueryAsync<int>(query, new { Ids = ids })).ToList();
            }

            foreach (var id in idsToProcess)
            {
                var jsonData = await _jsonDataService.GetJsonDataAsync(id);
                if (jsonData != null)
                {
                    var candidate = _jsonParser.ParseJsonData(jsonData);
                    var pdfPath = PdfGenerator.GeneratePdf(candidate);
                    pdfPaths.Add(pdfPath);

                    // Update IsStandardCVGenerated to 1
                    await _jsonDataService.UpdateIsStandardCVGeneratedAsync(id);
                }
            }

            if (!pdfPaths.Any())
            {
                return NotFound("No valid CV data found for the provided IDs");
            }

            // Zip the PDFs before sending
            var zipPath = Path.GetTempFileName() + ".zip";
            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                foreach (var pdfPath in pdfPaths)
                {
                    var entryName = Path.GetFileName(pdfPath);
                    var entry = archive.CreateEntry(entryName);

                    // Manually copy file content to zip entry
                    using (var entryStream = entry.Open())
                    using (var fileStream = new FileStream(pdfPath, FileMode.Open, FileAccess.Read))
                    {
                        await fileStream.CopyToAsync(entryStream);
                    }

                    System.IO.File.Delete(pdfPath); // Clean up individual files
                }
            }

            var zipBytes = await System.IO.File.ReadAllBytesAsync(zipPath);
            System.IO.File.Delete(zipPath); // Clean up the zip file after serving

            return File(zipBytes, "application/zip", "bulk_pdfs_by_ids.zip");
        }

        [HttpPost("generatePdfByQuantity")]
        public async Task<IActionResult> GeneratePdfByQuantity([FromBody] int quantity)
        {
            if (quantity <= 0)
            {
                return BadRequest("Quantity must be greater than zero.");
            }

            var pdfPaths = new List<string>();

            // Fetch IDs of records with IsStandardCVGenerated = 0 in a specific order
            var idsToProcess = new List<int>();
            using (var connection = new SqlConnection(_connectionString))
            {
                string query = "SELECT Id FROM tblParsedCVData WHERE IsStandardCVGenerated = 0 ORDER BY Id";
                idsToProcess = (await connection.QueryAsync<int>(query)).Take(quantity).ToList();
            }

            if (!idsToProcess.Any())
            {
                return NotFound("No valid CV data found for the provided quantity.");
            }

            Console.WriteLine($"Total IDs to process: {idsToProcess.Count}");

            var pdfGenerationTasks = idsToProcess.Select(async id =>
            {
                try
                {
                    Console.WriteLine($"Processing ID: {id}");
                    var jsonData = await _jsonDataService.GetJsonDataAsync(id);
                    if (jsonData != null)
                    {
                        var candidate = _jsonParser.ParseJsonData(jsonData);
                        var pdfPath = PdfGenerator.GeneratePdf(candidate);

                        // Update IsStandardCVGenerated to 1
                        using (var connection = new SqlConnection(_connectionString))
                        {
                            string updateQuery = "UPDATE tblParsedCVData SET IsStandardCVGenerated = 1 WHERE Id = @Id";
                            await connection.ExecuteAsync(updateQuery, new { Id = id });
                        }

                        Console.WriteLine($"PDF generated for ID: {id}");
                        return pdfPath;
                    }
                    else
                    {
                        Console.WriteLine($"No JSON data found for ID: {id}");
                    }
                }
                catch (Exception ex)
                {
                    // Log the exception for debugging purposes
                    Console.WriteLine($"Error processing ID {id}: {ex.Message}");
                }
                return null;
            });

            // Ensure tasks are awaited properly
            var pdfPathsResult = (await Task.WhenAll(pdfGenerationTasks)).Where(path => path != null).ToList();

            Console.WriteLine($"Total PDFs generated: {pdfPathsResult.Count}");

            if (!pdfPathsResult.Any())
            {
                return NotFound("No valid CV data found for the provided quantity.");
            }

            // Zip the PDFs before sending
            var zipPath = Path.GetTempFileName() + ".zip";
            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                foreach (var pdfPath in pdfPathsResult)
                {
                    var entryName = Path.GetFileName(pdfPath);
                    var entry = archive.CreateEntry(entryName);

                    // Manually copy file content to zip entry
                    using (var entryStream = entry.Open())
                    using (var fileStream = new FileStream(pdfPath, FileMode.Open, FileAccess.Read))
                    {
                        await fileStream.CopyToAsync(entryStream);
                    }

                    // Clean up individual files
                    System.IO.File.Delete(pdfPath);
                }
            }

            // Clean up the zip file after serving
            var zipBytes = await System.IO.File.ReadAllBytesAsync(zipPath);
            System.IO.File.Delete(zipPath);

            return File(zipBytes, "application/zip", "bulk_pdfs_by_quantity.zip");
        }

        //[HttpPost("generatePdfByQuantity")]
        //public async Task<IActionResult> GeneratePdfByQuantity([FromBody] int quantity)
        //{
        //    if (quantity <= 0)
        //    {
        //        return BadRequest("Quantity must be greater than zero.");
        //    }

        //    // Fetch IDs of records with IsStandardCVGenerated = 0 in a specific order
        //    var idsToProcess = new List<int>();
        //    using (var connection = new SqlConnection(_connectionString))
        //    {
        //        string query = "SELECT Id FROM tblParsedCVData WHERE IsStandardCVGenerated = 0 ORDER BY Id";
        //        idsToProcess = (await connection.QueryAsync<int>(query)).Take(quantity).ToList();
        //    }

        //    if (!idsToProcess.Any())
        //    {
        //        return NotFound("No valid CV data found for the provided quantity.");
        //    }

        //    // Temporary directory and zip file paths
        //    string tempDir = Path.Combine(Path.GetTempPath(), "pdfs");
        //    Directory.CreateDirectory(tempDir);

        //    string zipFilePath = Path.Combine(Path.GetTempPath(), "bulk_pdfs_by_quantity.zip");

        //    using (var zipToOpen = new FileStream(zipFilePath, FileMode.Create))
        //    using (var archive = new ZipArchive(zipToOpen, ZipArchiveMode.Create))
        //    {
        //        var pdfGenerationTasks = idsToProcess.Select(async id =>
        //        {
        //            try
        //            {
        //                var jsonData = await GetJsonDataAsync(id);
        //                if (jsonData != null)
        //                {
        //                    var candidateName = ExtractCandidateNameFromJson(jsonData);
        //                    var pdfBytes = CreatePdfFromJson(jsonData);

        //                    // Prepare file name with candidate's name
        //                    string fileName = ReplaceInvalidFileNameChars($"{candidateName}_{id}.pdf", '_');

        //                    // Add PDF to zip
        //                    var zipEntry = archive.CreateEntry(fileName);
        //                    using (var zipStream = zipEntry.Open())
        //                    {
        //                        await zipStream.WriteAsync(pdfBytes, 0, pdfBytes.Length);
        //                    }

        //                    // Update database to reflect PDF generation
        //                    await UpdateStatusAsync(id);
        //                }
        //            }
        //            catch (Exception ex)
        //            {
        //                // Log the exception for debugging purposes
        //                Console.WriteLine($"Error processing ID {id}: {ex.Message}");
        //            }
        //        });

        //        await Task.WhenAll(pdfGenerationTasks);
        //    }

        //    // Return zip file
        //    var fileBytes = await System.IO.File.ReadAllBytesAsync(zipFilePath);
        //    System.IO.File.Delete(zipFilePath); // Clean up zip file
        //    Directory.Delete(tempDir, true); // Clean up temp directory

        //    return File(fileBytes, "application/zip", "bulk_pdfs_by_quantity.zip");
        //}

        //private async Task<string> GetJsonDataAsync(int id)
        //{
        //    using (var connection = new SqlConnection(_connectionString))
        //    {
        //        var query = "SELECT ParsedData FROM tblParsedCVData WHERE Id = @Id";
        //        return await connection.QuerySingleOrDefaultAsync<string>(query, new { Id = id });
        //    }
        //}

        //private async Task UpdateStatusAsync(int id)
        //{
        //    using (var connection = new SqlConnection(_connectionString))
        //    {
        //        var query = "UPDATE tblParsedCVData SET IsStandardCVGenerated = 1 WHERE Id = @Id";
        //        await connection.ExecuteAsync(query, new { Id = id });
        //    }
        //}

        //private byte[] CreatePdfFromJson(string jsonData)
        //{
        //    // Your implementation to generate PDF from JSON
        //    // For demo purposes, this method should create a PDF from JSON data
        //    // and return its bytes.
        //    throw new NotImplementedException();
        //}

        //private string ExtractCandidateNameFromJson(string json)
        //{
        //    // Your implementation to extract candidate name from JSON
        //    // This method should parse JSON and extract the candidate's name.
        //    throw new NotImplementedException();
        //}

        //private string ReplaceInvalidFileNameChars(string fileName, char replacementChar)
        //{
        //    foreach (char c in Path.GetInvalidFileNameChars())
        //    {
        //        fileName = fileName.Replace(c, replacementChar);
        //    }
        //    return fileName;
        //}
    }
}
