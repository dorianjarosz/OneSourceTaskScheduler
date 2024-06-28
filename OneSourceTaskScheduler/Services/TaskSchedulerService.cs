using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OneSource.Data.Entities;
using OneSourceTaskScheduler.Data.Entities;
using OneSourceTaskScheduler.Repositories;
using OneSourceTaskScheduler.Security;
using OneSourceTaskScheduler.Services.Dtos;
using OneSourceTaskScheduler.Services.Utils;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;

namespace OneSourceTaskScheduler.Services
{
    public class TaskSchedulerService : ITaskSchedulerService
    {
        private readonly ILogger<TaskSchedulerService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IOneSourceRepository _repository;

        public TaskSchedulerService(IOneSourceRepository repository, ILogger<TaskSchedulerService> logger, IConfiguration configuration)
        {
            _repository = repository;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task ScheduleTasks(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Starting executing scheduled tasks.");

                    var activeScheduledTasks = await _repository.GetAsync<Schedules>(x => x.Active == true);

                    foreach (var activeTask in activeScheduledTasks)
                    {
                        string start;

                        Logs scheduleLog;

                        using (var context = await _repository.CreateDbContextAsync())
                        {
                            start = await (await _repository.GetQueryAsync<Schedules>(context))
                                .Where(r => r.taskName == activeTask.taskName)
                                .Select(r => r.Start).FirstOrDefaultAsync();

                            scheduleLog = await (await _repository.GetQueryAsync<Logs>(context))
                                .OrderByDescending(x => x.startTime)
                                .Where(x => x.taskTitle == activeTask.taskName)
                                .FirstOrDefaultAsync();
                        }

                        int checkStart;
                        Schedules currentTask;
                        int interval = 0;

                        if (int.TryParse(start, out checkStart))
                        {
                            interval = int.Parse(start);
                        }
                        else
                        {
                            interval = 1;
                        }

                        if (scheduleLog != null)
                        {

                            DateTime currentDateTime = DateTime.Now; // Current time

                            DateTime lastRecurrence = scheduleLog.startTime ?? DateTime.MinValue; // Last recurrence (default to MinValue if null)

                            DateTime nextRecurrence = lastRecurrence.AddMinutes(interval); // Next recurrence

                            if (currentDateTime >= nextRecurrence)
                            {
                                currentTask = activeTask;
                                await ExecuteTask(activeTask);

                            }
                            else
                            {
                                currentTask = null;
                            }
                        }
                        else
                        {
                            currentTask = activeTask;

                            await ExecuteTask(currentTask);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, ex.ToString());
                }

                _logger.LogInformation("Ended executing scheduled tasks.");

                _logger.LogInformation("Waiting the next minute to execute scheduled tasks further.");

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private async Task ExecuteTask(Schedules activeTask)
        {
            var taskdata = await _repository.GetOneAsync<Tasks>(task => task.taskName == activeTask.taskName);
            dynamic result = "";

            if (activeTask == null)
            {
                Console.WriteLine("Schedule table id null!");
            }
            else
            {
                if (activeTask.Source != "SQL Script")
                {
                    using (var context = await _repository.CreateDbContextAsync())
                    {
                        result = await (await _repository.GetQueryAsync<Tasks>(context))
               .Join(
                  (await _repository.GetQueryAsync<Schedules>(context)).Where(s => s.taskName == activeTask.taskName),
                   t1 => t1.taskName,
                   t2 => t2.taskName,
                   (t1, t2) => new { Table1 = t1, Table2 = t2 })
               .Select(t => new { t.Table1.taskName, t.Table1.SystemType, t.Table2.Start, t.Table2.Recurrence, t.Table2.End })
               .FirstOrDefaultAsync();
                    }
                }

                if (activeTask.Source == "SQL Script")
                {
                    using (var context = await _repository.CreateDbContextAsync())
                    {
                        result = await (await _repository.GetQueryAsync<Tasks>(context))
                    .Join(
                        (await _repository.GetQueryAsync<Schedules>(context)).Where(s => s.taskName == activeTask.taskName),
                        t1 => t1.taskName,
                        t2 => t2.taskName,
                        (t1, t2) => new { Table1 = t1, Table2 = t2 })
                    .Join(
                        (await _repository.GetQueryAsync<Scripts>(context)),
                        t => t.Table1.taskName,
                        t3 => t3.TaskName,
                        (t, t3) => new { t.Table1, t.Table2, Table3 = t3 })
                    .Select(t => new { t.Table1.taskName, t.Table1.SystemType, t.Table2.Start, t.Table3.Script, t.Table2.Recurrence, t.Table2.End })
                    .FirstOrDefaultAsync();
                    }
                }
            }

            if (result != null)
            {
                bool hasProperty = result.GetType().GetProperty("Recurrence") != null;
                if (hasProperty)
                {
                    var startedTaskDateTime = await TaskStartLog(activeTask.taskName);

                    string EndwithoutUtc = "";
                    string resultEnd = result.End;
                    if (result.End != "Never" && !string.IsNullOrWhiteSpace(result.End))
                    {
                        string[] SplitEnd = result.End.Split(" ");
                        EndwithoutUtc = SplitEnd[0] + " " + SplitEnd[1];
                    }

                    else
                    {
                        EndwithoutUtc = result.End;
                    }

                    string[] parts = result.Recurrence.Split(' ');
                    string timezone = parts[parts.Length - 1];

                    try
                    {

                        if (result.Recurrence.StartsWith("Everyday"))
                        {
                            await RunTaskEveryDay(result.taskName, result.SystemType, EndwithoutUtc, result.Start, timezone, startedTaskDateTime);
                        }
                        else if (result.Recurrence.StartsWith("Everyweek"))
                        {
                            await RunTaskEveryWeek(result.taskName, result.SystemType, EndwithoutUtc, result.Start, timezone, startedTaskDateTime);
                        }
                        else if (result.Recurrence.StartsWith("Everymonth"))
                        {
                            await RunTaskEveryMonth(result.taskName, result.SystemType, EndwithoutUtc, result.Start, timezone, startedTaskDateTime);
                        }
                        else if (result.Recurrence.EndsWith("minute") || result.Recurrence.EndsWith("minutes"))
                        {
                            string[] Endparts = result.End.Split(' ');
                            string Endtimezone = Endparts[Endparts.Length - 1];
                            await RunTaskEveryMinute(result.taskName, result.SystemType, EndwithoutUtc, result.Start, Endtimezone, startedTaskDateTime);
                        }
                        else if (result.Recurrence.EndsWith("hour") || result.Recurrence.EndsWith("hours"))
                        {
                            string[] Endparts = result.End.Split(' ');
                            string Endtimezone = Endparts[Endparts.Length - 1];
                            await RunTaskEveryHour(result.taskName, result.SystemType, EndwithoutUtc, result.Start, Endtimezone, startedTaskDateTime);
                        }
                        else if (result.Recurrence == "On demand")
                        {
                            await RunTaskOnDemand(result.taskName, startedTaskDateTime);

                            activeTask.Active = false;
                            await _repository.UpdateAsync(activeTask);
                        }
                        else
                        {
                            string[] splitRecurrence = result.Recurrence.Split(' ');
                            string recurrence = splitRecurrence[0] + " " + splitRecurrence[1];
                            await RunTaskOnce(result.taskName, recurrence, timezone, startedTaskDateTime);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (result.Recurrence.StartsWith("Everyday") ||
                            result.Recurrence.StartsWith("Everyweek") ||
                            result.Recurrence.StartsWith("Everymonth") ||
                            result.Recurrence.EndsWith("minute") ||
                            result.Recurrence.EndsWith("minutes") ||
                            result.Recurrence.EndsWith("hour") ||
                            result.Recurrence.EndsWith("hours"))
                        {
                            await LogsUpdate(result.taskName, startedTaskDateTime, false, ex.Message, ex.StackTrace);
                        }
                        else
                        {
                            await LogsUpdate(result.taskName, startedTaskDateTime, false, ex.Message, ex.StackTrace);

                            activeTask.Active = false;
                            await _repository.UpdateAsync(activeTask);
                        }
                    }

                }
                else
                {
                    Console.WriteLine("Recurrence is not exist!");
                }

            }
            else
            {
                Console.WriteLine("Recurrence is not exist!");
            }
        }

        private async Task RunTaskOnce(string taskName, string recurrence, string timezone, DateTime startedTaskDateTime)
        {

            DateTime taskDate = await ConvertTimeZone(recurrence, timezone);
            string TaskDateformat = "yyyy-MM-dd HH:mm";
            DateTime TaskRecurrence = DateTime.ParseExact(recurrence, TaskDateformat, CultureInfo.InvariantCulture);
            DateTime currentDateTime = DateTime.Now;
            DateTime currentDateTimeWithoutSeconds = new DateTime(currentDateTime.Year, currentDateTime.Month, currentDateTime.Day, currentDateTime.Hour, currentDateTime.Minute, 0);
            if (taskDate <= currentDateTimeWithoutSeconds)
            {
                var TaskInfo = await _repository.GetOneAsync<Tasks>(t => t.taskName == taskName);
                if (TaskInfo.SystemType == "BoldChat")
                {
                    await RunBoldTasks(taskName, startedTaskDateTime);
                }
                else if (TaskInfo.SystemType == "Nice")
                {
                    await RunNiceTasks(taskName, startedTaskDateTime);
                }
                else if (TaskInfo.SystemType == "ServiceNow")
                {
                    await RunSnowTasks(taskName, startedTaskDateTime);
                }
                else if (TaskInfo.SystemType == "SQL Script")
                {
                    await RunScriptTasks(taskName, startedTaskDateTime);
                }

                var schedules = await _repository.GetAsync<Schedules>(s => s.taskName == taskName);

                foreach (var schedule in schedules)
                {
                    schedule.Active = false;
                }

                await _repository.UpdateRangeAsync(schedules);
            }
        }

        private async Task RunBoldTasks(string taskName, DateTime startedTaskDateTime)
        {
            DateTime now = DateTime.Now;
            string clientID = "";
            string folderID = "";
            string fromDate = "";
            string endDate = "";
            var StartedTask = startedTaskDateTime;

            var url = "";
            var task = await _repository.GetOneAsync<Tasks>(t => t.taskName == taskName);
            var login = (await _repository.GetOneAsync<Tasks>(t => t.taskName == taskName)).Credential;
            var apiInfo = await _repository.GetOneAsync<Api>(x => x.CustomerName == task.CustomerName && x.ApiName == task.APIName);
            string parameters = task.fromDate.ToString();
            string[] param = parameters.Split(", ");
            if (task.fromDate.EndsWith("hours"))
            {
                string[] hour = task.fromDate.Split(" ");
                DateTime lastHour = now.AddHours(-int.Parse(hour[1]));
                fromDate = lastHour.ToString("yyyy-MM-ddTHH:mm:ss");
                endDate = now.ToString("yyyy-MM-ddTHH:mm:ss");
                folderID = task.FolderID;
                clientID = task.ClientID;

            }
            else if (task.fromDate.EndsWith("minutes"))
            {
                string[] minute = task.fromDate.Split(" ");
                DateTime lastMinute = now.AddMinutes(-int.Parse(minute[1]));
                fromDate = lastMinute.ToString("yyyy-MM-ddTHH:mm:ss");
                endDate = now.ToString("yyyy-MM-ddTHH:mm:ss");
                folderID = task.FolderID;
                clientID = task.ClientID;
            }
            else if (task.fromDate.EndsWith("days"))
            {
                string[] day = task.fromDate.Split(" ");
                DateTime lastDay = now.AddDays(-int.Parse(day[1]));
                fromDate = lastDay.ToString("yyyy-MM-ddT00:00:00");
                DateTime endOfLastDay = now.Date.AddDays(-1).Add(new TimeSpan(23, 59, 59));
                endDate = endOfLastDay.ToString("yyyy-MM-ddTHH:mm:ss");
                folderID = task.FolderID;
                clientID = task.ClientID;

            }
            else if (task.fromDate.EndsWith("weeks"))
            {
                string[] week = task.fromDate.Split(" ");

                DateTime startOfLastNWeeks = now.AddDays(-7 * int.Parse(week[1]) - (int)now.DayOfWeek + 1);
                DateTime endOfLastNWeeks = startOfLastNWeeks.AddDays(7 * int.Parse(week[1]) - 2).AddDays(1).AddSeconds(-1);

                fromDate = startOfLastNWeeks.ToString("yyyy-MM-ddT00:00:00");
                endDate = endOfLastNWeeks.ToString("yyyy-MM-ddT23:59:59");
                folderID = task.FolderID;
                clientID = task.ClientID;
            }
            else if (task.fromDate.StartsWith("today"))
            {
                DateTime today = DateTime.Today;
                DateTime startOfDay = today.Date; // Set time to 00:00:00
                DateTime endOfDay = today.Date.AddDays(1).AddTicks(-1); // Set time to 23:59:59.9999999

                // You can also format the datetime as per your requirement:
                fromDate = startOfDay.ToString("yyyy-MM-ddTHH:mm:ss");
                endDate = endOfDay.ToString("yyyy-MM-ddTHH:mm:ss");
                folderID = task.FolderID;
                clientID = task.ClientID;
            }
            else if (task.fromDate.StartsWith("after"))
            {
                string[] date = task.fromDate.Split(" ");
                fromDate = date[1] + "T" + date[2];
                DateTime start = DateTime.ParseExact(fromDate, "yyyy-MM-ddTHH:mm:ss", null);
                DateTime currentDateTime = DateTime.Now;
                endDate = currentDateTime.ToString("yyyy-MM-ddTHH:mm:ss");
                folderID = task.FolderID;
                clientID = task.ClientID;

            }
            else if (string.IsNullOrEmpty(task.fromDate))
            {
                clientID = task.ClientID;
            }

            else
            {
                folderID = task.FolderID;
                clientID = task.ClientID;
                fromDate = task.fromDate;
                endDate = task.endDate;
            }
            if (task.APIName == "getTextChats")
            {

                url = $"{apiInfo.EndPointPath}?clientID={clientID}&folderID={folderID}&fromDate={fromDate}&endDate={endDate}&login={login}";

            }
            else if (task.APIName == "getInactiveChats")
            {
                url = $"{apiInfo.EndPointPath}?clientID={clientID}&folderID={folderID}&fromDate={fromDate}&endDate={endDate}&login={login}";
            }
            else
            {
                url = $"{apiInfo.EndPointPath}?clientID={clientID}";
            }

            var systemInfo = await _repository.GetOneAsync<Systems>(x => x.CustomerName == task.CustomerName);
            string username = systemInfo.OneSourceLogin;
            string password = systemInfo.OneSourcePassword;
            string credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(username + ":" + password));

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            HttpResponseMessage response;

            using var httpClient = new HttpClient();
            response = await httpClient.SendAsync(request);

            string strUrl = "";

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();

                HttpClient client = new HttpClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

                if (task.APIName == "getTextChats")
                {
                    strUrl = apiInfo.EndPointPath;
                }
                else if (task.APIName == "getInactiveChats")
                {
                    strUrl = apiInfo.EndPointPath;
                }
                else if (task.APIName == "getOperators")
                {
                    strUrl = apiInfo.EndPointPath;
                }



                client.BaseAddress = new Uri(strUrl);
                var json = JsonConvert.SerializeObject(content);
                var Result = new StringContent(json, Encoding.UTF8, "application/json");
                if (task.APIName == "getOperators")
                {
                    var jsonClientId = JsonConvert.SerializeObject(clientID);
                    var ClientIDData = new StringContent(jsonClientId, Encoding.UTF8, "application/json");
                    HttpResponseMessage responses = await client.PostAsync(strUrl, ClientIDData);
                }
                else
                {
                    HttpResponseMessage responses = await client.PostAsync(strUrl, Result);
                }


                await LogsUpdate(taskName, StartedTask);
            }
        }

        private async Task RunNiceTasks(string taskName, DateTime startedTaskDateTime)
        {
            DateTime now = DateTime.Now;
            string campaignId = "";
            string fromDate = "";
            string endDate = "";

            DateTime StartedTask;

            var url = "";
            var task = await _repository.GetOneAsync<Tasks>(t => t.taskName == taskName);

            var system = await _repository.GetOneAsync<Systems>(x => x.CustomerName == task.CustomerName
                                && x.System == task.SystemType && x.SystemType == task.EnvironmentNames);
            var apiInfo = await _repository.GetOneAsync<Api>(s => s.System == system.System && s.CustomerName == system.CustomerName &&
                                                s.SystemType == system.SystemType);
            string parameters = task.fromDate?.ToString() ?? "";
            string[] param = parameters.Split(", ");

            if (task.TimeRangeType != null && task.TimeRange != null)
            {
                DateTime startDate;

                switch (task.TimeRangeType)
                {
                    case "minutes":
                        {
                            startDate = DateTime.Now.AddMinutes(-task.TimeRange.Value);
                            break;
                        }
                    case "hours":
                        {
                            startDate = DateTime.Now.AddHours(-task.TimeRange.Value);
                            break;
                        }
                    case "days":
                        {
                            startDate = DateTime.Now.AddDays(-task.TimeRange.Value);
                            break;
                        }
                    default:
                        {
                            throw new Exception("Invalid time range type. Must be either of minutes, hours or days.");
                        }
                }

                fromDate = startDate.ToString("yyyy-MM-ddTHH:mm");
                endDate = DateTime.Now.ToString("yyyy-MM-ddTHH:mm");
                campaignId = task.ClientID;
            }
            else if (task.fromDate.EndsWith("hours"))
            {
                string[] hour = task.fromDate.Split(" ");
                DateTime lastHour = now.AddHours(-int.Parse(hour[1]));
                fromDate = lastHour.ToString("yyyy-MM-ddTHH:mm");
                endDate = "updatedSince";
                campaignId = task.ClientID;

            }
            else if (task.fromDate.EndsWith("minutes"))
            {
                string[] minute = task.fromDate.Split(" ");
                DateTime lastMinute = now.AddMinutes(-int.Parse(minute[1]));
                fromDate = lastMinute.ToString("yyyy-MM-ddTHH:mm");
                endDate = "updatedSince";
                campaignId = task.ClientID;
            }
            else if (task.fromDate.EndsWith("days"))
            {
                string[] day = task.fromDate.Split(" ");
                DateTime lastDay = now.AddDays(-int.Parse(day[1]));
                fromDate = lastDay.ToString("yyyy-MM-ddT00:00");
                DateTime endOfLastDay = now.Date.AddDays(-1).Add(new TimeSpan(23, 59, 59));
                endDate = "updatedSince";
                campaignId = task.ClientID;

            }
            else if (task.fromDate.EndsWith("weeks"))
            {
                string[] week = task.fromDate.Split(" ");

                DateTime startOfLastNWeeks = now.AddDays(-7 * int.Parse(week[1]) - (int)now.DayOfWeek + 1);
                DateTime endOfLastNWeeks = startOfLastNWeeks.AddDays(7 * int.Parse(week[1]) - 2).AddDays(1).AddSeconds(-1);

                fromDate = startOfLastNWeeks.ToString("yyyy-MM-ddT00:00");
                endDate = "updatedSince";
                campaignId = task.ClientID;
            }
            else if (task.fromDate.StartsWith("today"))
            {
                DateTime today = DateTime.Today;
                DateTime startOfDay = today.Date;
                DateTime endOfDay = today.Date.AddDays(1).AddTicks(-1);

                fromDate = startOfDay.ToString("yyyy-MM-ddTHH:mm");
                endDate = endOfDay.ToString("yyyy-MM-ddTHH:mm");
                campaignId = task.ClientID;
            }
            else if (task.fromDate.StartsWith("after"))
            {
                string[] date = task.fromDate.Split(" ");
                fromDate = date[1] + "T" + date[2];
                DateTime start = DateTime.ParseExact(fromDate, "yyyy-MM-ddTHH:mm", null);
                DateTime currentDateTime = DateTime.Now;
                endDate = "updatedSince";
                campaignId = task.ClientID;

            }
            else
            {
                campaignId = task.ClientID;
                fromDate = task.fromDate;
                endDate = task.endDate;
            }

            string mediaTypeQuery;

            if (task.additionalQuery.Contains("mediaType=3") || task.additionalQuery.Contains("mediaType=4"))
                mediaTypeQuery = "&" + task.additionalQuery.TrimStart('^');
            else
                mediaTypeQuery = "&mediaType=3";

            url = $"{apiInfo.EndPointPath}?startDate={fromDate}Z{(endDate == "updatedSince" ? ("") : ("&endDate=" + endDate))}&Z{mediaTypeQuery}&campaignId={campaignId}";

            using (var httpClient = new HttpClient())
            {
                string username = system.OneSourceLogin;
                string password = SecurityUtils.HashString(task.OneSourcePassword);
                string credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(username + ":" + password));

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
                var response = await httpClient.SendAsync(request);

                string strUrl = "";

                if (response.IsSuccessStatusCode)
                {
                    string username2 = task.OneSourceLogin;
                    string password2 = SecurityUtils.HashString(task.OneSourcePassword);
                    string credentials2 = Convert.ToBase64String(Encoding.ASCII.GetBytes(username2 + ":" + password2));

                    var content = await response.Content.ReadAsStringAsync();

                    if (string.IsNullOrWhiteSpace(content))
                    {
                        _logger.LogInformation("No data have been returned by the Nice API");
                        await LogsUpdate(taskName, startedTaskDateTime, true);
                        return;
                    }

                    using (var httpClient2 = new HttpClient())
                    {
                        httpClient2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials2);

                        if (task.DestinationServiceOption == "localInstance")
                        {
                            strUrl = apiInfo.EndPointPath;
                        }
                        else if (task.DestinationServiceOption == "externalInstance")
                        {
                            var customer = await _repository.GetOneAsync<Customers>(x => x.CustomerName == task.CustomerName);

                            strUrl = customer.OneSourceURL + task.ServiceAPIEndpoint;
                        }
                        else if (task.DestinationServiceOption == "otherInstance")
                        {
                            strUrl = task.CustomServiceUrl + task.ServiceAPIEndpoint;
                        }

                        httpClient2.BaseAddress = new Uri(strUrl);
                        var json = JsonConvert.SerializeObject(new { table = "NICE_Interactions", data = content });
                        var result = new StringContent(json, Encoding.UTF8, "application/json");
                        HttpResponseMessage responses = await httpClient2.PostAsync(strUrl, result);
                        await LogsUpdate(taskName, startedTaskDateTime, responses.IsSuccessStatusCode);
                    }

                }
                else
                {
                    throw new Exception("Nice API returned with the response: " + response.StatusCode);
                }
            }

        }

        private async Task RunScriptTasks(string taskName, DateTime startedTaskDateTime)
        {
            var scriptInfo = await _repository.GetOneAsync<Scripts>(s => s.TaskName == taskName);
            string Script = await GetScriptAsync(taskName);
            await _repository.ExecuteSqlRawAsync(Script);
            await LogsUpdate(taskName, startedTaskDateTime);

            async Task<string> GetScriptAsync(string taskName)
            {
                var script = (await _repository.GetOneAsync<Scripts>(s => s.TaskName == taskName)).Script;

                return script.ToString();
            }
        }
        private async Task RunSnowTasks(string taskName, DateTime startedTaskDateTime)
        {
            var taskInfo = await _repository.GetOneAsync<Tasks>(t => t.taskName == taskName);

            var reloadDataInBatchesDto = new ReloadDataInBatchesDto
            {
                TaskName = taskName,
                Customer = taskInfo.CustomerName,
                SourceTableName = taskInfo.SourceTable,
                SelectedDestTable = taskInfo.DestinationTable,
                Operation = taskInfo.DateOperation,
                FromDate = DateTime.Parse(taskInfo.fromDate),
                EndDate = DateTime.Parse(taskInfo.endDate),
                DateField = taskInfo.DatesList,
                BatchSize = taskInfo.BatchSize,
                AdditionalQuery = taskInfo.additionalQuery,
                FavSelected = taskInfo.FavouriteQuery,
                FilterName = taskInfo.FilterName,
                FieldsList = taskInfo.FieldsList
            };

            var reloadDataInBatchesResultDto = await ReloadDataInBatches(reloadDataInBatchesDto);

            var succeeded = reloadDataInBatchesResultDto.Succeeded;

            await LogsUpdate(taskName, startedTaskDateTime, succeeded, reloadDataInBatchesResultDto.ExceptionMessage, reloadDataInBatchesResultDto.StackTrace, reloadDataInBatchesResultDto.ProcessedTicketsCount);
        }
        private async Task RunTaskRecurrent(string taskName, string system, string end, DateTime startedTaskDateTime)
        {
            string TaskDateformat = "yyyy-MM-dd HH:mm";
            if (end != "Never" && end != "")
            {
                DateTime TaskDate = DateTime.ParseExact(end, TaskDateformat, CultureInfo.InvariantCulture);
                if (TaskDate >= DateTime.Now)
                {
                    if (system == "BoldChat")
                    {
                        await RunBoldTasks(taskName, startedTaskDateTime);
                    }
                    else if (system == "SQL Script")
                    {
                        await RunScriptTasks(taskName, startedTaskDateTime);
                    }
                    else if (system == "Nice")
                    {
                        await RunNiceTasks(taskName, startedTaskDateTime);
                    }
                    else if (system == "ServiceNow")
                    {
                        await RunSnowTasks(taskName, startedTaskDateTime);
                    }

                    var schedules = await _repository.GetAsync<Schedules>(s => s.taskName == taskName);
                    foreach (var item in schedules)
                    {
                        item.Active = false;
                    }

                    await _repository.UpdateRangeAsync(schedules);
                }
            }
            else if (end == "Never" && end != "")
            {
                if (system == "BoldChat")
                {
                    await RunBoldTasks(taskName, startedTaskDateTime);
                }
                else if (system == "SQL Script")
                {
                    await RunScriptTasks(taskName, startedTaskDateTime);
                }
                else if (system == "Nice")
                {
                    await RunNiceTasks(taskName, startedTaskDateTime);
                }
            }
        }

        private async Task RunTaskEveryDay(string taskName, string system, string end, string start, string timezone, DateTime startedTaskDateTime)
        {
            DateTime taskDate = await ConvertTimeZoneByHour(start, timezone);
            DateTime TaskEndDate = DateTime.Now;
            if (end != "Never" && end != "")
            {
                TaskEndDate = await ConvertEndTimeZone(end, timezone);
            }

            DateTime currentDateTime = DateTime.Now;
            DateTime currentDateTimeWithoutSeconds = new DateTime(currentDateTime.Year, currentDateTime.Month, currentDateTime.Day, currentDateTime.Hour, currentDateTime.Minute, 0);

            if (taskDate.TimeOfDay == currentDateTimeWithoutSeconds.TimeOfDay)
            {
                if (end != "Never" && end != "")
                {
                    if (TaskEndDate >= DateTime.Now)
                    {
                        if (system == "BoldChat")
                        {
                            await RunBoldTasks(taskName, startedTaskDateTime);
                        }
                        else if (system == "SQL Script")
                        {
                            await RunScriptTasks(taskName, startedTaskDateTime);
                        }
                        else if (system == "Nice")
                        {
                            await RunNiceTasks(taskName, startedTaskDateTime);
                        }
                        else if (system == "ServiceNow")
                        {
                            await RunSnowTasks(taskName, startedTaskDateTime);
                        }

                    }
                    else
                    {
                        var schedules = await _repository.GetAsync<Schedules>(s => s.taskName == taskName);
                        foreach (var item in schedules)
                        {
                            item.Active = false;
                        }
                        await _repository.UpdateRangeAsync(schedules);
                    }
                }
                else if (end == "Never" && end != "")
                {
                    if (system == "BoldChat")
                    {
                        await RunBoldTasks(taskName, startedTaskDateTime);
                    }
                    else if (system == "SQL Script")
                    {
                        await RunScriptTasks(taskName, startedTaskDateTime);
                    }
                    else if (system == "Nice")
                    {
                        await RunNiceTasks(taskName, startedTaskDateTime);
                    }
                    else if (system == "ServiceNow")
                    {
                        await RunSnowTasks(taskName, startedTaskDateTime);
                    }
                }
            }
            DateTime CheckEndDate = DateTime.Now;
            if (end != "Never" && end != "" && TaskEndDate < CheckEndDate)
            {
                var schedules = await _repository.GetAsync<Schedules>(s => s.taskName == taskName);
                foreach (var item in schedules)
                {
                    item.Active = false;
                }

                await _repository.UpdateRangeAsync(schedules);
            }

        }

        private async Task RunTaskEveryWeek(string taskName, string system, string end, string start, string timezone, DateTime startedTaskDateTime)
        {
            string[] dates = start.Split(",");

            string convertedDateTime = await ConvertTimeZoneByHourAndDay(dates[0], dates[1], timezone);

            string[] dateSplit = convertedDateTime.Split(",");
            DateTime TaskEndDate = DateTime.Now;
            if (end != "Never" && end != "")
            {
                TaskEndDate = await ConvertEndTimeZone(end, timezone);
            }
            DateTime parsedTime = DateTime.ParseExact(dateSplit[1].Replace(" ", ""), "HH:mm", CultureInfo.InvariantCulture);
            DateTime currentDateTime = DateTime.Now;
            DateTime currentDateTimeWithoutSeconds = new DateTime(currentDateTime.Year, currentDateTime.Month, currentDateTime.Day, currentDateTime.Hour, currentDateTime.Minute, 0);

            if (parsedTime.TimeOfDay == currentDateTimeWithoutSeconds.TimeOfDay)
            {
                DayOfWeek inputDayOfWeek;
                if (Enum.TryParse(dateSplit[0].Replace(" ", ""), true, out inputDayOfWeek))
                {
                    DayOfWeek currentDayOfWeek = DateTime.Today.DayOfWeek;
                    if (inputDayOfWeek == currentDayOfWeek)
                    {
                        string TaskEndDateformat = "yyyy-MM-dd HH:mm";
                        if (end != "Never" && end != "")
                        {
                            if (TaskEndDate >= DateTime.Now)
                            {
                                if (system == "BoldChat")
                                {
                                    await RunBoldTasks(taskName, startedTaskDateTime);
                                }
                                else if (system == "SQL Script")
                                {
                                    await RunScriptTasks(taskName, startedTaskDateTime);
                                }
                                else if (system == "Nice")
                                {
                                    await RunNiceTasks(taskName, startedTaskDateTime);
                                }
                                else if (system == "ServiceNow")
                                {
                                    await RunSnowTasks(taskName, startedTaskDateTime);
                                }

                            }
                            else
                            {
                                var ScheduleInfo = await _repository.GetOneAsync<Schedules>(s => s.taskName == taskName);
                                ScheduleInfo.Active = false;

                                await _repository.UpdateAsync(ScheduleInfo);
                            }
                        }
                        else if (end == "Never" && end != "")
                        {
                            if (system == "BoldChat")
                            {
                                await RunBoldTasks(taskName, startedTaskDateTime);
                            }
                            else if (system == "SQL Script")
                            {
                                await RunScriptTasks(taskName, startedTaskDateTime);
                            }
                            else if (system == "Nice")
                            {
                                await RunNiceTasks(taskName, startedTaskDateTime);
                            }
                            else if (system == "ServiceNow")
                            {
                                await RunSnowTasks(taskName, startedTaskDateTime);
                            }
                        }
                    }
                }

            }

            else if (end != "Never" && end != "" && parsedTime.TimeOfDay < currentDateTimeWithoutSeconds.TimeOfDay)
            {
                var scheduleInfo = await _repository.GetOneAsync<Schedules>(s => s.taskName == taskName);
                scheduleInfo.Active = false;

                await _repository.UpdateAsync(scheduleInfo);
            }

        }

        private async Task RunTaskEveryMonth(string taskName, string system, string end, string start, string timezone, DateTime startedTaskDateTime)
        {
            string[] dates = start.Split(",");

            int dayOfMonth = int.Parse(dates[0]);
            string convertedDateTime = await ConvertTimeByDayOfMonth(dayOfMonth, dates[1], timezone);
            string[] DateSplit = convertedDateTime.Split(",");
            DateTime TaskEndDate = DateTime.Now;
            if (end != "Never" && end != "")
            {
                TaskEndDate = await ConvertEndTimeZone(end, timezone);
            }
            DateTime parsedTime = DateTime.ParseExact(DateSplit[1], "HH:mm", CultureInfo.InvariantCulture);
            DateTime currentDateTime = DateTime.Now;
            DateTime currentDateTimeWithoutSeconds = new DateTime(currentDateTime.Year, currentDateTime.Month, currentDateTime.Day, currentDateTime.Hour, currentDateTime.Minute, 0);

            if (parsedTime.TimeOfDay == currentDateTimeWithoutSeconds.TimeOfDay)
            {

                int currentDay = DateTime.Now.Day;
                int TaskDay = int.Parse(DateSplit[0]);

                DayOfWeek currentDayOfWeek = DateTime.Today.DayOfWeek;
                if (currentDay == TaskDay)
                {
                    string TaskEndDateformat = "yyyy-MM-dd HH:mm";
                    if (end != "Never" && end != "")
                    {
                        if (TaskEndDate >= DateTime.Now)
                        {
                            if (system == "BoldChat")
                            {
                                await RunBoldTasks(taskName, startedTaskDateTime);
                            }
                            else if (system == "SQL Script")
                            {
                                await RunScriptTasks(taskName, startedTaskDateTime);
                            }
                            else if (system == "Nice")
                            {
                                await RunNiceTasks(taskName, startedTaskDateTime);
                            }
                            else if (system == "ServiceNow")
                            {
                                await RunSnowTasks(taskName, startedTaskDateTime);
                            }

                        }
                        else
                        {
                            var schedules = await _repository.GetAsync<Schedules>(s => s.taskName == taskName);
                            foreach (var item in schedules)
                            {
                                item.Active = false;
                            }
                            await _repository.UpdateRangeAsync(schedules);
                        }
                    }
                    else if (end == "Never" && end != "")
                    {
                        if (system == "BoldChat")
                        {
                            await RunBoldTasks(taskName, startedTaskDateTime);
                        }
                        else if (system == "SQL Script")
                        {
                            await RunScriptTasks(taskName, startedTaskDateTime);
                        }
                        else if (system == "Nice")
                        {
                            await RunNiceTasks(taskName, startedTaskDateTime);
                        }
                        else if (system == "ServiceNow")
                        {
                            await RunSnowTasks(taskName, startedTaskDateTime);
                        }
                    }
                }
            }
            else if (end != "Never" && end != "" && parsedTime.TimeOfDay < currentDateTimeWithoutSeconds.TimeOfDay)
            {
                var scheduleInfo = await _repository.GetOneAsync<Schedules>(s => s.taskName == taskName);
                scheduleInfo.Active = false;
                await _repository.UpdateAsync(scheduleInfo);
            }

        }

        private async Task RunTaskEveryMinute(string taskName, string system, string end, string start, string timezone, DateTime startedTaskDateTime)
        {
            DateTime taskEndDate = DateTime.Now;
            if (end != "Never" && end != "")
            {
                taskEndDate = await ConvertEndTimeZone(end, timezone);
            }

            if (end != "Never" && end != "")
            {
                if (taskEndDate >= DateTime.Now)
                {
                    if (system == "BoldChat")
                    {
                        await RunBoldTasks(taskName, startedTaskDateTime);
                    }
                    else if (system == "SQL Script")
                    {
                        await RunScriptTasks(taskName, startedTaskDateTime);
                    }
                    else if (system == "Nice")
                    {
                        await RunNiceTasks(taskName, startedTaskDateTime);
                    }
                    else if (system == "ServiceNow")
                    {
                        await RunSnowTasks(taskName, startedTaskDateTime);
                    }
                }
                else
                {
                    var schedules = await _repository.GetAsync<Schedules>(s => s.taskName == taskName);

                    foreach (var item in schedules)
                    {
                        item.Active = false;
                    }
                    await _repository.UpdateRangeAsync(schedules);
                }
            }
            else if (end == "Never" && end != "")
            {
                if (system == "BoldChat")
                {
                    await RunBoldTasks(taskName, startedTaskDateTime);
                }
                else if (system == "SQL Script")
                {
                    await RunScriptTasks(taskName, startedTaskDateTime);
                }
                else if (system == "Nice")
                {
                    await RunNiceTasks(taskName, startedTaskDateTime);
                }
                else if (system == "ServiceNow")
                {
                    await RunSnowTasks(taskName, startedTaskDateTime);
                }
            }
        }

        private async Task RunTaskEveryHour(string taskName, string system, string end, string start, string timezone, DateTime startedTaskDateTime)
        {
            DateTime taskEndDate = DateTime.Now;
            if (end != "Never" && end != "")
            {
                taskEndDate = await ConvertEndTimeZone(end, timezone);
            }

            if (end != "Never" && end != "")
            {
                if (taskEndDate >= DateTime.Now)
                {
                    if (system == "BoldChat")
                    {
                        await RunBoldTasks(taskName, startedTaskDateTime);
                    }
                    else if (system == "SQL Script")
                    {
                        await RunScriptTasks(taskName, startedTaskDateTime);
                    }
                    else if (system == "Nice")
                    {
                        await RunNiceTasks(taskName, startedTaskDateTime);
                    }
                    else if (system == "ServiceNow")
                    {
                        await RunSnowTasks(taskName, startedTaskDateTime);
                    }

                }
                else
                {
                    var schedules = await _repository.GetAsync<Schedules>(s => s.taskName == taskName);
                    foreach (var item in schedules)
                    {
                        item.Active = false;
                    }
                    await _repository.UpdateRangeAsync(schedules);
                }
            }
            else if (end == "Never" && end != "")
            {
                if (system == "BoldChat")
                {
                    await RunBoldTasks(taskName, startedTaskDateTime);
                }
                else if (system == "SQL Script")
                {
                    await RunScriptTasks(taskName, startedTaskDateTime);
                }
                else if (system == "Nice")
                {
                    await RunNiceTasks(taskName, startedTaskDateTime);
                }
                else if (system == "ServiceNow")
                {
                    await RunSnowTasks(taskName, startedTaskDateTime);
                }
            }

        }
        private async Task RunTaskOnDemand(string taskName, DateTime startedTaskDateTime)
        {
            var taskInfo = await _repository.GetOneAsync<Tasks>(t => t.taskName == taskName);

            if (taskInfo.SystemType == "BoldChat")
            {
                await RunBoldTasks(taskName, startedTaskDateTime);
            }
            else if (taskInfo.SystemType == "Nice")
            {
                await RunNiceTasks(taskName, startedTaskDateTime);
            }
            else if (taskInfo.SystemType == "ServiceNow")
            {
                await RunSnowTasks(taskName, startedTaskDateTime);

            }
            else if (taskInfo.SystemType == "SQL Script")
            {
                await RunScriptTasks(taskName, startedTaskDateTime);
            }
        }

        private async Task<DateTime> TaskStartLog(string taskName)
        {
            var task = await _repository.GetOneAsync<Tasks>(t => t.taskName == taskName);
            DateTime now = DateTime.Now;
            var log = new Logs
            {
                taskTitle = task.taskName,
                application = task.SystemType,
                customer = task.CustomerName,
                startTime = now,
                endTime = null,
                message = "Task in progress",
                status = "Task is started.",
                LastUpdate = DateTime.Now
            };
            await _repository.AddAsync<Logs>(log);

            return now;
        }

        private async Task LogsUpdate(string taskName, DateTime startedTask, bool succeeded = true, string exceptionMessage = null, string stackTrace = null, string processedTasksCount = null)
        {
            var log = await _repository.GetOneAsync<Logs>(l => l.taskTitle == taskName && l.startTime == startedTask);
            log.endTime = DateTime.Now;
            log.status = $"Task is completed{(succeeded ? "" : " with an error")}.";
            log.message = $"Task is completed{(succeeded ? "" : " with an error")}.";
            log.LastUpdate = DateTime.Now;
            log.ProcessedTicketsCount = processedTasksCount;

            log.ExceptionMessage = exceptionMessage;
            log.StackTrace = stackTrace;

            await _repository.UpdateAsync(log);
        }

        private async Task<DateTime> ConvertTimeZone(string taskDate, string timezone)
        {
            // Parse the datetime
            DateTime datetime = DateTime.Parse(taskDate);

            // Parse the timezone offset
            int startIndex = timezone.IndexOf('(') + 1;
            int endIndex = timezone.IndexOf(')');
            string timezonePart = timezone.Substring(startIndex, endIndex - startIndex);
            string check = timezonePart.Replace("UTC", "");

            if (check.StartsWith("+"))
            {
                check = check.Replace("+", "");
            }

            string[] offsetParts = check.Split(':', ' ');
            int hours = int.Parse(offsetParts[0]);
            int minutes = int.Parse(offsetParts[1]);
            TimeSpan offset = new TimeSpan(hours, minutes, 0);

            string timeZoneID = ConvertOffsetToTimeZoneID(offset);

            if (timeZoneID == null)
            {
                // Handle case where no time zone ID matches the offset
                // Return or throw an appropriate response
            }

            TimeZoneInfo targetTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneID);

            DateTimeOffset datetimeOffset = new DateTimeOffset(datetime, offset);

            DateTime currentDatetime;


            if (targetTimeZone.IsDaylightSavingTime(datetimeOffset))
            {
                currentDatetime = TimeZoneInfo.ConvertTime(datetimeOffset, targetTimeZone).DateTime;
            }
            else
            {
                currentDatetime = datetimeOffset.ToLocalTime().DateTime;
            }



            TimeZoneInfo systemTimeZone = TimeZoneInfo.Local;
            TimeSpan offsetSystem = systemTimeZone.BaseUtcOffset;

            if (offsetSystem == targetTimeZone.BaseUtcOffset)
            {
                currentDatetime = datetime;
            }

            await Task.Delay(0); // Optional delay to demonstrate an async operation

            return currentDatetime;
        }

        private async Task<DateTime> ConvertEndTimeZone(string taskDate, string timezone)
        {
            // Parse the datetime
            DateTime datetime = DateTime.Parse(taskDate);

            // Parse the timezone offset
            int startIndex = timezone.IndexOf('(') + 1;
            int endIndex = timezone.IndexOf(')');
            string timezonePart = timezone.Substring(startIndex, endIndex - startIndex);
            string check = timezonePart.Replace("UTC", "");

            if (check.StartsWith("+"))
            {
                check = check.Replace("+", "");
            }

            string[] offsetParts = check.Split(':', ' ');
            int hours = int.Parse(offsetParts[0]);
            int minutes = int.Parse(offsetParts[1]);
            TimeSpan offset = new TimeSpan(hours, minutes, 0);

            string timeZoneID = ConvertOffsetToTimeZoneID(offset);

            if (timeZoneID == null)
            {
                // Handle case where no time zone ID matches the offset
                // Return or throw an appropriate response
            }

            TimeZoneInfo targetTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneID);

            DateTimeOffset datetimeOffset = new DateTimeOffset(datetime, offset);

            DateTime currentDatetime;


            if (targetTimeZone.IsDaylightSavingTime(datetimeOffset))
            {
                currentDatetime = TimeZoneInfo.ConvertTime(datetimeOffset, targetTimeZone).DateTime;
            }
            else
            {
                currentDatetime = datetimeOffset.ToLocalTime().DateTime;
            }



            TimeZoneInfo systemTimeZone = TimeZoneInfo.Local;
            TimeSpan offsetSystem = systemTimeZone.BaseUtcOffset;

            if (offsetSystem == targetTimeZone.BaseUtcOffset)
            {
                currentDatetime = datetime;
            }

            await Task.Delay(0); // Optional delay to demonstrate an async operation

            return currentDatetime;
        }

        private string ConvertOffsetToTimeZoneID(TimeSpan offset)
        {
            ReadOnlyCollection<TimeZoneInfo> timeZones = TimeZoneInfo.GetSystemTimeZones();

            foreach (TimeZoneInfo timeZone in timeZones)
            {
                if (timeZone.BaseUtcOffset == offset)
                {
                    return timeZone.Id;
                }
            }

            return null; // Offset does not match any time zone
        }

        public async Task<DateTime> ConvertTimeZoneByHour(string timeString, string timezone)
        {
            // Parse the time
            DateTime time = DateTime.ParseExact(timeString, "HH:mm", CultureInfo.InvariantCulture);

            string offsetString = timezone.Replace("(", "").Replace("UTC", "").Replace("+", "").Replace(")", "");

            // Parse the timezone offset
            TimeSpan offset = TimeSpan.Parse(offsetString);

            string timeZoneID = ConvertOffsetToTimeZoneID(offset);

            // Find the target time zone by ID
            TimeZoneInfo targetTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneID);

            // Create a DateTimeOffset object with the time and the target time zone
            DateTimeOffset dateTimeOffset = new DateTimeOffset(time, offset);

            // Convert to the current system's local time
            DateTime currentDatetime = dateTimeOffset.ToOffset(DateTimeOffset.Now.Offset).DateTime;

            TimeZoneInfo systemTimeZone = TimeZoneInfo.Local;
            TimeSpan offsetSystem = systemTimeZone.BaseUtcOffset;

            if (offsetSystem == targetTimeZone.BaseUtcOffset)
            {
                currentDatetime = time;
            }

            await Task.Delay(0); // Optional delay to demonstrate an async operation

            return currentDatetime;
        }

        public async Task<string> ConvertTimeZoneByHourAndDay(string timeString, string dayOfWeekString, string timezoneOffset)
        {
            // Parse the time
            TimeSpan time = TimeSpan.Parse(timeString);

            // Parse the day of the week
            DayOfWeek targetDayOfWeek = (DayOfWeek)Enum.Parse(typeof(DayOfWeek), dayOfWeekString, true);

            // Parse the timezone offset
            string offsetString = timezoneOffset.Replace("(", "").Replace("UTC", "").Replace("+", "").Replace(")", "");
            TimeSpan offset = TimeSpan.Parse(offsetString);

            // Get the current system timezone
            TimeZoneInfo systemTimeZone = TimeZoneInfo.Local;

            // Get the current system datetime in the system timezone
            DateTimeOffset currentSystemDateTimeOffset = DateTimeOffset.Now;
            DateTime currentSystemDateTime = currentSystemDateTimeOffset.DateTime;

            // Get the current day of the week in the system timezone
            DayOfWeek currentSystemDayOfWeek = currentSystemDateTime.DayOfWeek;

            // Calculate the difference in days between the target day of the week and the current day of the week
            int differenceInDays = targetDayOfWeek - currentSystemDayOfWeek;

            // Adjust the difference if it's negative (target day is in the previous week)
            if (differenceInDays < 0)
            {
                differenceInDays += 7;
            }

            // Calculate the adjusted datetime by adding or subtracting the difference in days
            DateTime adjustedDateTime = currentSystemDateTime.AddDays(differenceInDays);

            // Combine the adjusted datetime with the target time
            DateTime combinedDateTime = adjustedDateTime.Date.Add(time);





            // Create a DateTimeOffset object with the combined datetime and the timezone offset
            DateTimeOffset targetDateTimeOffset = new DateTimeOffset(combinedDateTime, offset);

            // Convert the DateTimeOffset to the system timezone
            DateTimeOffset systemDateTimeOffset = TimeZoneInfo.ConvertTime(targetDateTimeOffset, systemTimeZone);


            string timeZoneID = ConvertOffsetToTimeZoneID(offset);

            // Find the target time zone by ID
            TimeZoneInfo targetTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneID);

            TimeSpan offsetSystem = systemTimeZone.BaseUtcOffset;

            string output = "";
            if (offsetSystem == targetTimeZone.BaseUtcOffset)
            {
                output = string.Format("{0}, {1:HH:mm}", targetDateTimeOffset.DayOfWeek, targetDateTimeOffset);
            }

            else
            {
                output = string.Format("{0}, {1:HH:mm}", systemDateTimeOffset.DayOfWeek, systemDateTimeOffset);
            }
            // Format the resulting DateTime object as a string in the desired format

            await Task.Delay(0); // Optional delay to demonstrate an async operation

            return output;
        }

        public async Task<string> ConvertTimeByDayOfMonth(int dayOfMonth, string timeString, string timezoneOffset)
        {
            // Parse the time
            TimeSpan time = TimeSpan.Parse(timeString);

            // Get the current system timezone
            TimeZoneInfo systemTimeZone = TimeZoneInfo.Local;

            // Get the current system datetime in the system timezone
            DateTimeOffset currentSystemDateTimeOffset = DateTimeOffset.Now;
            DateTime currentSystemDateTime = currentSystemDateTimeOffset.DateTime;

            // Get the current month and year
            int currentMonth = currentSystemDateTime.Month;
            int currentYear = currentSystemDateTime.Year;

            // Calculate the target datetime based on the given day of the month and time
            DateTime targetDateTime = new DateTime(currentYear, currentMonth, dayOfMonth, time.Hours, time.Minutes, time.Seconds);

            // Parse the timezone offset
            string offsetString = timezoneOffset.Replace("(", "").Replace("UTC", "").Replace("+", "").Replace(")", "");
            TimeSpan offset = TimeSpan.Parse(offsetString);

            // Create a DateTimeOffset object with the target datetime and the timezone offset
            DateTimeOffset targetDateTimeOffset = new DateTimeOffset(targetDateTime, offset);

            // Convert the target DateTimeOffset to the system timezone
            DateTimeOffset systemDateTimeOffset = TimeZoneInfo.ConvertTime(targetDateTimeOffset, systemTimeZone);



            string timeZoneID = ConvertOffsetToTimeZoneID(offset);

            // Find the target time zone by ID
            TimeZoneInfo targetTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneID);

            TimeSpan offsetSystem = systemTimeZone.BaseUtcOffset;

            string output = "";
            if (offsetSystem == targetTimeZone.BaseUtcOffset)
            {
                output = string.Format("{0},{1:HH:mm}", systemDateTimeOffset.Day, targetDateTimeOffset);
            }

            else
            {
                output = string.Format("{0},{1:HH:mm}", systemDateTimeOffset.Day, systemDateTimeOffset);
            }

            // Format the resulting DateTime object as a string in the desired format

            await Task.Delay(0); // Optional delay to demonstrate an async operation

            return output;
        }

        private string GetUrlQuery(string operation, string fromDate, string endDate, string dateField, string additionalQuery, string globalFilter)
        {
            if (additionalQuery != "" && additionalQuery != null)
            {
                additionalQuery = additionalQuery.TrimStart('^', ' ');
            }

            string[] operationsArr = { ">=", ">", "BETWEEN", "<", ">=javascript:gs.beginningOfToday()", ">=javascript:gs.beginningOfYesterday()", ">=javascript:gs.beginningOfLast3Months()", ">=javascript:gs.beginningOfLast15Minutes()" };

            string urlQueryValue = "";

            string urlQuery = "";

            string urlQueryKey = "&sysparm_query=";

            bool operationAllowed = operationsArr.Contains(operation);
            if (operationAllowed)
            {
                if (operation == "BETWEEN")
                {
                    urlQueryValue = string.Format("{0}BETWEENjavascript:gs.dateGenerate('{1}')" + "@" + "javascript:gs.dateGenerate('{2}')", dateField, fromDate, endDate);
                }
                else if (operation.IndexOf(">=javascript:gs.beginningOf") > -1)
                {
                    urlQueryValue = string.Format("{0}{1}", dateField, operation);
                }
                else
                {
                    urlQueryValue = string.Format("{0}{1}javascript:gs.dateGenerate('{2}')", dateField, operation, fromDate);
                }
            }

            if (urlQueryValue != "")
            {
                urlQuery = urlQueryKey + urlQueryValue;
            }

            if (additionalQuery != "")
            {
                urlQuery += "^" + additionalQuery;
            }
            if (globalFilter != "")
            {
                urlQuery += "^" + globalFilter.TrimStart('^', ' ');
            }

            return urlQuery;
        }

        private async Task<WebRequest> ConnectSnowv4(string url, string Customer, string systemLogin = null, string systemPassword = null)
        {
            var system = await _repository.GetOneAsync<Systems>(s => s.CustomerName == Customer);
            string result = "";
            WebRequest tRequest;

            string username = systemLogin ?? system.SystemLogin;
            string password = systemPassword ?? system.SystemLogin;
            tRequest = WebRequest.Create(url);
            tRequest.Method = "get";
            tRequest.ContentType = "application/json";
            tRequest.Headers.Add("User-Agent", "AtosOneSource");
            String encoded = System.Convert.ToBase64String(System.Text.Encoding.GetEncoding("ISO-8859-1").GetBytes(username + ":" + password));
            tRequest.Headers.Add("Authorization", "Basic " + encoded);

            return tRequest;
        }

        private async Task<WebRequest> ConnectSnowv4(string url, string Customer)
        {
            var system = await _repository.GetOneAsync<Systems>(s => s.CustomerName == Customer);
            string result = "";
            WebRequest tRequest;

            string username = system.SystemLogin;
            string password = system.SystemPassword;
            tRequest = WebRequest.Create(url);
            tRequest.Method = "get";
            tRequest.ContentType = "application/json";
            tRequest.Timeout = 100000;
            String encoded = System.Convert.ToBase64String(System.Text.Encoding.GetEncoding("ISO-8859-1").GetBytes(username + ":" + password));
            tRequest.Headers.Add("Authorization", "Basic " + encoded);

            return tRequest;
        }

        private LinkHeader LinksFromHeader(string linkHeaderStr)
        {
            LinkHeader linkHeader = null;

            if (!string.IsNullOrWhiteSpace(linkHeaderStr))
            {
                linkHeaderStr = linkHeaderStr.Replace(",<", ",<<");
                Regex regex = new Regex(",<");
                string[] linkStrings = regex.Split(linkHeaderStr);

                if (linkStrings != null && linkStrings.Any())
                {
                    linkHeader = new LinkHeader();

                    foreach (string linkString in linkStrings)
                    {
                        var relMatch = Regex.Match(linkString, "(?<=rel=\").+?(?=\")", RegexOptions.IgnoreCase);
                        var linkMatch = Regex.Match(linkString, "(?<=<).+?(?=>)", RegexOptions.IgnoreCase);

                        if (relMatch.Success && linkMatch.Success)
                        {
                            string rel = relMatch.Value.ToUpper();
                            string link = linkMatch.Value;

                            switch (rel)
                            {
                                case "FIRST":
                                    linkHeader.FirstLink = link;
                                    break;
                                case "PREV":
                                    linkHeader.PrevLink = link;
                                    break;
                                case "NEXT":
                                    linkHeader.NextLink = link;
                                    break;
                                case "LAST":
                                    linkHeader.LastLink = link;
                                    break;
                            }
                        }
                    }
                }
            }

            return linkHeader;
        }

        private async Task<ReloadDataInBatchesResultDto> ReloadDataInBatches(ReloadDataInBatchesDto dto)
        {
            var resultDto = new ReloadDataInBatchesResultDto();

            var loadDataFromFirstBatchDto = new LoadDataFromFirstBatchDto
            {
                SourceTableName = dto.SourceTableName,
                SelectedDestTable = dto.SelectedDestTable,
                Customer = dto.Customer,
                BatchSize = dto.BatchSize,
                AdditionalQuery = dto.AdditionalQuery,
                DateField = dto.DateField,
                EndDate = dto.EndDate,
                FavSelected = dto.FavSelected,
                FieldsList = dto.FieldsList,
                FromDate = dto.FromDate,
                Operation = dto.Operation,
                FilterName = dto.FilterName,
                TaskName = dto.TaskName
            };

            var loadDataFromFirstBatchResultDto = await LoadDataFromFirstBatch(loadDataFromFirstBatchDto);

            resultDto.ProcessedTicketsCount = loadDataFromFirstBatchResultDto.UpdateLog;

            if (!loadDataFromFirstBatchResultDto.Succeeded)
            {
                resultDto.Succeeded = loadDataFromFirstBatchResultDto.Succeeded;
                resultDto.ExceptionMessage = loadDataFromFirstBatchResultDto.ExceptionMessage;
                resultDto.StackTrace = loadDataFromFirstBatchResultDto.StackTrace;

                return resultDto;
            }

            if (string.IsNullOrWhiteSpace(loadDataFromFirstBatchResultDto.NextLink))
            {
                resultDto.Succeeded = loadDataFromFirstBatchResultDto.Succeeded;
                return resultDto;
            }


            var loadDataFromNextBatchDto = new LoadDataFromNextBatchDto
            {
                TaskName = dto.TaskName,
                Iteration = 1,
                NextLink = loadDataFromFirstBatchResultDto.NextLink,
                CurrentOffset = loadDataFromFirstBatchResultDto.CurrentOffset,
                Customer = loadDataFromFirstBatchDto.Customer,
                SelectedDestTable = loadDataFromFirstBatchDto.SelectedDestTable,
                SourceTableName = loadDataFromFirstBatchDto.SourceTableName,

            };

            for (int i = loadDataFromNextBatchDto.Iteration; i < 5; i++)
            {
                if (loadDataFromNextBatchDto.Iteration < 5 &&
                    !string.IsNullOrWhiteSpace(loadDataFromNextBatchDto.NextLink))
                {
                    var loadDataFromNextBatchResultDto = await LoadDataFromNextBatch(loadDataFromNextBatchDto);

                    if (!loadDataFromNextBatchResultDto.Succeeded)
                    {
                        resultDto.ExceptionMessage = loadDataFromNextBatchResultDto.ExceptionMessage;
                        resultDto.StackTrace = loadDataFromNextBatchResultDto.StackTrace;
                        resultDto.Succeeded = false;
                        return resultDto;
                    }
                    else
                    {
                        resultDto.ProcessedTicketsCount = loadDataFromNextBatchResultDto.UpdateLog;

                        loadDataFromNextBatchDto.Iteration = loadDataFromNextBatchResultDto.Iteration;
                        loadDataFromNextBatchDto.NextLink = loadDataFromNextBatchResultDto.NextLink;
                        loadDataFromNextBatchDto.CurrentOffset = loadDataFromNextBatchResultDto.CurrentOffset;
                    }
                }
                else
                {
                    resultDto.Succeeded = true;
                    return resultDto;
                }
            }

            resultDto.Succeeded = true;
            return resultDto;
        }

        private async Task<LoadDataFromFirstBatchResultDto> LoadDataFromFirstBatch(LoadDataFromFirstBatchDto dto)
        {
            LoadDataFromFirstBatchResultDto resultDto = new LoadDataFromFirstBatchResultDto();

            int count = 0;

            string totalCount = "";

            try
            {
                dto.SourceTableName = HtmlSanitizer.sanitize(dto.SourceTableName);
                dto.SelectedDestTable = HtmlSanitizer.sanitize(dto.SelectedDestTable);
                dto.FavSelected = HtmlSanitizer.sanitize(dto.FavSelected);

                SnowApiFilter filter = null;

                if (dto.FilterName != null)
                {
                    filter = await _repository.GetOneAsync<SnowApiFilter>(x => x.FilterName == dto.FilterName && x.SnowTableTechnical == dto.SourceTableName);

                    if (filter == null)
                    {
                        throw new NullReferenceException($"The '{dto.FilterName}' filter does not exist in the database.");
                    }
                }

                var task = await _repository.GetOneAsync<Tasks>(x => x.taskName == dto.TaskName);

                var customer = await _repository.GetOneAsync<Customers>(x => x.CustomerName == dto.Customer);

                string urlQuery = GetUrlQuery(operation: dto.Operation,
                    fromDate: dto.FromDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    endDate: dto.EndDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    dateField: dto.DateField,
                    additionalQuery: dto.AdditionalQuery,
                    globalFilter: filter?.Query ?? "");

                var tableConfigEntity = (await _repository.GetAsync<SNOWApiTableConfiguration, ICollection<SNOWApiColumnConfiguration>>(x => x.TechnicalTableName == dto.SourceTableName && x.SqlTableName == dto.SelectedDestTable, x => x.SnowTableColumnConfigurations)).FirstOrDefault();

                var configEntities = await _repository.GetAsync<Configuration>(x => x.Name == "SnowUrl");

                var system = await _repository.GetOneAsync<Systems>(s => s.CustomerName == task.CustomerName && s.System == task.SystemType && s.SystemType == task.EnvironmentNames);

                string urlDomain = system.RootURL;

                string urlfields = "&sysparm_fields=" + dto.FieldsList;

                string urlAPI = string.Format("/api/now/table/{0}?sysparm_display_value=" + tableConfigEntity.Param.ToString().ToLower() + "&sysparm_exclude_reference_link=true", dto.SourceTableName);

                string urlLimitationData;

                urlLimitationData = "&sysparm_limit=" + (dto.BatchSize != null ? dto.BatchSize : 500);

                string urlOffset = "&sysparm_offset=0";

                string nextLink = urlDomain + urlAPI + urlQuery + urlfields + urlOffset + urlLimitationData;

                string headers = "";
                string last = "";

                string systemLogin;
                string systemPassword;

                if (system.SystemLogin == task.Credential)
                {
                    systemLogin = task.Credential;
                    systemPassword = system.SystemPassword;
                }
                else
                {
                    var systemCreds = await _repository.GetOneAsync<SystemCredentials>(x => x.SystemLogin == task.Credential);

                    systemLogin = systemCreds.SystemLogin;
                    systemPassword = systemCreds.SystemPassword;
                }

                var request = await ConnectSnowv4(nextLink, dto.Customer, systemLogin, systemPassword);

                HttpWebResponse response;

                try
                {
                    response = (HttpWebResponse)request.GetResponse();

                    _logger.LogDebug("Successfully returned response from the link: " + nextLink);
                }
                catch (WebException ex)
                {
                    _logger.LogError(ex, ex.ToString());
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, ex.ToString());
                    throw;
                }

                string sResponseFromServer;
                using (Stream stream = response.GetResponseStream())
                {
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        sResponseFromServer = reader.ReadToEnd();
                    }
                }

                headers = response.GetResponseHeader("Link");
                totalCount = response.GetResponseHeader("X-Total-Count");
                var link = LinksFromHeader(headers);
                if (headers == "")
                {
                    nextLink = null;
                }
                else
                {
                    nextLink = link.NextLink;
                    last = link.LastLink;
                }

                var responseJson = JObject.Parse(sResponseFromServer);

                count = responseJson["result"].Count();

                if (count == 0)
                {
                    resultDto.Succeeded = true;
                    resultDto.NextLink = "";
                    resultDto.UpdateLog = "0/0";
                    resultDto.TotalCount = 0;
                    resultDto.CurrentOffset = 0;

                    _logger.LogInformation("Processed " + resultDto.UpdateLog + $" SNOW tickets by the task '{dto.TaskName}'.");

                    return resultDto;
                }

                string snowResponsestr = JsonConvert.SerializeObject(responseJson["result"]);

                bool succeded = await UpdateDB(tableConfigEntity.TableNameVariable, snowResponsestr, task, customer);

                resultDto.Succeeded = succeded;

                resultDto.NextLink = nextLink;
                resultDto.UpdateLog = count.ToString() + "/" + totalCount;
                resultDto.TotalCount = int.Parse(totalCount);
                resultDto.CurrentOffset = count;

                if (resultDto.Succeeded)
                    _logger.LogInformation("Processed " + resultDto.UpdateLog + $" SNOW tickets by the task '{dto.TaskName}'.");
                else
                    _logger.LogError("Tried to process " + resultDto.UpdateLog + $" SNOW tickets by the task '{dto.TaskName}', but an error occured during the operation.");

                return resultDto;
            }
            catch (Exception ex)
            {
                int totalCountInt = 0;

                if (!string.IsNullOrEmpty(totalCount))
                {
                    totalCountInt = int.Parse(totalCount);
                }
                else
                {
                    totalCountInt = count;
                }

                resultDto.UpdateLog = count + "/" + totalCountInt;
                resultDto.TotalCount = totalCountInt;
                resultDto.CurrentOffset = count;

                resultDto.ExceptionMessage = ex.Message;
                resultDto.StackTrace = ex.StackTrace;

                _logger.LogError(ex, ex.Message);

                _logger.LogError("Error occured, but processed " + resultDto.UpdateLog + $" SNOW tickets by the task '{dto.TaskName}'.");

                return resultDto;
            }
        }

        private async Task<LoadDataFromNextBatchResultDto> LoadDataFromNextBatch(LoadDataFromNextBatchDto dto)
        {
            LoadDataFromNextBatchResultDto resultDto = new LoadDataFromNextBatchResultDto { };

            int count = dto.CurrentOffset;

            int currentCount = dto.CurrentOffset;

            string totalCount = "";

            try
            {
                int currentIteration = 1;
                string headers;
                string last;

                string nextURL = "";
                string nextLink = "";
                if (!string.IsNullOrWhiteSpace(dto.NextLink))
                {
                    nextLink = dto.NextLink;
                    count = dto.CurrentOffset;
                    currentIteration = dto.Iteration + 1;

                }

                if (currentIteration > 100)
                {
                    resultDto.Iteration = currentIteration;
                    resultDto.NextLink = nextURL;
                    resultDto.CurrentOffset = count;

                    resultDto.Succeeded = true;

                    return resultDto;
                }

                if (!string.IsNullOrWhiteSpace(nextLink))
                {
                    var task = await _repository.GetOneAsync<Tasks>(x => x.taskName == dto.TaskName);

                    var customer = await _repository.GetOneAsync<Customers>(x => x.CustomerName == dto.Customer);

                    var system = await _repository.GetOneAsync<Systems>(s => s.CustomerName == task.CustomerName && s.System == task.SystemType && s.SystemType == task.EnvironmentNames);

                    string systemLogin;
                    string systemPassword;

                    if (system.SystemLogin == task.Credential)
                    {
                        systemLogin = task.Credential;
                        systemPassword = system.SystemPassword;
                    }
                    else
                    {
                        var systemCreds = await _repository.GetOneAsync<SystemCredentials>(x => x.SystemLogin == task.Credential);

                        systemLogin = systemCreds.SystemLogin;
                        systemPassword = systemCreds.SystemPassword;
                    }

                    var request = await ConnectSnowv4(nextLink, dto.Customer, systemLogin, systemPassword);

                    HttpWebResponse response;

                    try
                    {
                        response = (HttpWebResponse)request.GetResponse();
                    }
                    catch (WebException ex)
                    {
                        _logger.LogError(ex, ex.ToString());
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, ex.ToString());
                        throw;
                    }

                    string sResponseFromServer;

                    using (Stream stream = response.GetResponseStream())
                    {
                        using (StreamReader reader = new StreamReader(stream))
                        {
                            sResponseFromServer = reader.ReadToEnd();
                        }
                    }

                    headers = response.GetResponseHeader("Link");
                    totalCount = response.GetResponseHeader("X-Total-Count");
                    var link = LinksFromHeader(headers);
                    if (headers == "")
                    {
                        nextURL = null;
                    }
                    else
                    {
                        nextURL = link.NextLink;
                        last = link.LastLink;
                    }

                    var responseJson = JObject.Parse(sResponseFromServer);

                    count += responseJson["result"].Count();

                    resultDto.Iteration = currentIteration;
                    resultDto.NextLink = nextURL;
                    resultDto.UpdateLog = count.ToString() + "/" + totalCount;
                    resultDto.CurrentOffset = count;

                    string snowResponsestr = JsonConvert.SerializeObject(responseJson["result"]);

                    var tableConfigEntity = await _repository.GetOneAsync<SNOWApiTableConfiguration>(x => x.TechnicalTableName == dto.SourceTableName && x.SqlTableName == dto.SelectedDestTable);

                    bool succeded = await UpdateDB(tableConfigEntity.TableNameVariable, snowResponsestr, task, customer);

                    if (succeded)
                        currentCount += count;

                    resultDto.Succeeded = succeded;

                    if (resultDto.Succeeded)
                        _logger.LogInformation("Processed " + resultDto.UpdateLog + $" SNOW tickets by the task '{dto.TaskName}'.");
                    else
                        _logger.LogError("Tried to process " + resultDto.UpdateLog + $" SNOW tickets by the task '{dto.TaskName}', but an error occured during the operation.");

                    return resultDto;
                }
                else
                {
                    resultDto.Iteration = currentIteration;
                    resultDto.NextLink = nextURL;
                    resultDto.CurrentOffset = count;

                    resultDto.Succeeded = true;

                    return resultDto;
                }
            }
            catch (Exception ex)
            {
                int totalCountInt = 0;

                if (!string.IsNullOrEmpty(totalCount))
                {
                    totalCountInt = int.Parse(totalCount);
                }

                resultDto.UpdateLog = currentCount + "/" + totalCountInt;
                resultDto.CurrentOffset = currentCount;
                resultDto.Succeeded = false;
                resultDto.ExceptionMessage = ex.Message;
                resultDto.StackTrace = ex.StackTrace;

                _logger.LogError(ex, ex.Message);

                _logger.LogError("Error occured, bu processed " + resultDto.UpdateLog + $" SNOW tickets by the task '{dto.TaskName}'.");

                return resultDto;
            }
        }

        private async Task<bool> UpdateDB(string oneSourceTable, string snowData, Tasks task = null, Customers customer = null)
        {
            HttpClient httpClient = new HttpClient();

            string snowLogin;
            string snowPassword;

            if (task != null)
            {
                snowLogin = task.OneSourceLogin;
                if (task.DestinationServiceOption == "localInstance")
                    snowPassword = SecurityUtils.HashString(task.OneSourcePassword);
                else
                    snowPassword = task.OneSourcePassword;
            }
            else
            {
                snowLogin = "OneSourceSyncService";
                var user = await _repository.GetOneAsync<OneSourceUser>(x => x.Login == snowLogin);
                snowPassword = user.Password;
            }

            string destinationUrl = null;

            if (task != null && customer != null)
            {
                if (task.DestinationServiceOption == "localInstance")
                {
                    destinationUrl = _configuration["DomainUri"] + "/api/snow";
                }
                else if (task.DestinationServiceOption == "externalInstance")
                {
                    destinationUrl = customer.OneSourceURL + task.ServiceAPIEndpoint;
                }
                else if (task.DestinationServiceOption == "otherInstance")
                {
                    destinationUrl = task.CustomServiceUrl + task.ServiceAPIEndpoint;
                }
            }
            else
            {
                destinationUrl = _configuration["DomainUri"] + "/api/snow";
            }

            string credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(snowLogin + ":" + snowPassword));

            if (task.DestinationServiceOption == "localInstance")
            {
                var json = JsonConvert.SerializeObject(new { table = oneSourceTable, data = snowData });
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);

                var response = await httpClient.PostAsync(destinationUrl, content);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Request to the URL '" + destinationUrl + "' ended with " + response.StatusCode + " status code");
                }

                return response.StatusCode == HttpStatusCode.OK;
            }
            else
            {
                NameValueCollection payload = new NameValueCollection();
                payload.Add("table", oneSourceTable);
                payload.Add("data", snowData);
                try
                {
                    using (WebClient client = new WebClient())
                    {
                        client.Headers.Add("Authorization", "Basic " + credentials);
                        var response = client.UploadValues(destinationUrl, payload);

                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Response error returned from the external service endpoint: " + destinationUrl);
                    return false;
                }
            }
        }
    }
}
