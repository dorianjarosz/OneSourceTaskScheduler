namespace OneSourceTaskScheduler.Services.Dtos
{
    public class ReloadDataInBatchesResultDto
    {
        public string ProcessedTicketsCount { get; set; }
        public bool Succeeded { get; set; }

        public string ExceptionMessage { get; set; }

        public string StackTrace { get; set; }
    }
}
