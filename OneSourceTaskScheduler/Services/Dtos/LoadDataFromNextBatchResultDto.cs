namespace OneSourceTaskScheduler.Services.Dtos
{
    public class LoadDataFromNextBatchResultDto
    {
        public int Iteration { get; set; }

        public string NextLink { get; set; }

        public string UpdateLog { get; set; }

        public int CurrentOffset { get; set; }

        public string ExceptionMessage { get; set; }

        public string StackTrace { get; set; }

        public bool Succeeded { get; set; }
    }
}
