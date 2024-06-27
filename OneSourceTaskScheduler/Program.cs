using Microsoft.EntityFrameworkCore;
using OneSourceTaskScheduler;
using OneSourceTaskScheduler.Data;
using OneSourceTaskScheduler.Repositories;
using OneSourceTaskScheduler.Services;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.Configure<HostOptions>(hostOptions =>
        {
            hostOptions.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
        });

        services.AddDbContextFactory<OneSourceContext>(options =>
                    options.UseSqlServer(context.Configuration.GetConnectionString("OneSourceContextConnection")));

        services.AddSingleton<IOneSourceRepository, OneSourceRepository>();
        services.AddSingleton<ITaskSchedulerService, TaskSchedulerService>();

        services.AddHostedService<TaskSchedulerWorker>();

        services.AddWindowsService(options =>
        {
            options.ServiceName = "One Source Task Scheduler";
        });
    })
    .UseWindowsService()
    .Build();

host.Run();
