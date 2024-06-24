namespace OneSourceTaskScheduler.Services
{
    public interface ITaskSchedulerService
    {
        Task ScheduleTasks(CancellationToken stoppingToken);
    }
}
