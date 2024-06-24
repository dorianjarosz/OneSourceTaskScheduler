using OneSourceTaskScheduler.Services;

namespace OneSourceTaskScheduler
{
    public class TaskSchedulerWorker : BackgroundService
    {
        private readonly ILogger<TaskSchedulerWorker> _logger;
        private readonly ITaskSchedulerService _taskSchedulerService;

        public TaskSchedulerWorker(ILogger<TaskSchedulerWorker> logger, ITaskSchedulerService taskSchedulerService)
        {
            _logger = logger;
            _taskSchedulerService = taskSchedulerService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await _taskSchedulerService.ScheduleTasks(stoppingToken);
        }
    }
}
