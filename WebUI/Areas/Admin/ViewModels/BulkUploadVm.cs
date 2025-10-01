using Microsoft.AspNetCore.Http;
using System.Collections.Generic;

namespace WebUI.Areas.Admin.ViewModels
{
    public class BulkUploadVm
    {
        public IFormFile CsvFile { get; set; }

        // Seçenekler
        public bool AutoCreateDepartments { get; set; }
        public bool AutoCreateJobTitles { get; set; }
        public bool SkipIfEmailExists { get; set; }

        // Özet
        public int TotalRows { get; set; }
        public int CreatedEmployees { get; set; }
        public int SkippedRows { get; set; }
        public int CreatedDepartments { get; set; }
        public int CreatedJobTitles { get; set; }
        public string? Message { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();

        // Satır bazlı sonuç listesi (UI için)
        public List<RowResult> RowResults { get; set; } = new();
    }

    public class RowResult
    {
        public int LineNo { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }

        public RowResult() { }
        public RowResult(int lineNo, bool success, string message)
        {
            LineNo = lineNo;
            Success = success;
            Message = message;
        }
    }
}
