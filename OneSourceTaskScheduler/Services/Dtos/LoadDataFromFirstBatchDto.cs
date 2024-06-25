namespace OneSourceTaskScheduler.Services.Dtos
{
    public class LoadDataFromFirstBatchDto
    {
        public string TaskName { get; set; }
        public string Customer { get; set; }
        public string SourceTableName { get; set; }
        public string SelectedDestTable { get; set; }
        public string Operation { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime EndDate { get; set; }
        public string DateField { get; set; }
        public int? BatchSize { get; set; }
        public string AdditionalQuery { get; set; }
        public string FavSelected { get; set; }
        public string FilterName { get; set; }

        public string FieldsList { get; set; }
    }
}
