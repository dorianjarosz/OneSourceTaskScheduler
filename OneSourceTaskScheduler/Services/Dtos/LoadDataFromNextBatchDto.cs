namespace OneSourceTaskScheduler.Services.Dtos
{
    public class LoadDataFromNextBatchDto
    {
        public string TaskName { get; set; }
        public string NextLink { get; set; }

        public int CurrentOffset { get; set; }

        public int Iteration { get; set; }

        public string Customer { get; set; }

        public string SourceTableName { get; set; }

        public string SelectedDestTable { get; set; }

    }
}
