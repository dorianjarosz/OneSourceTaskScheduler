namespace OneSourceTaskScheduler.Services.Dtos
{
    public class LoadDataFromFirstBatchResultDto
    {
        public string UpdateLog { get; set; }
        public string NextLink { get; set; }
        public int CurrentOffset { get; set; }
        public int TotalCount { get; set; }
        public bool Succeeded { get; set; }

        public string ExceptionMessage { get; set; }

        public string StackTrace { get; set; }
    }
}
