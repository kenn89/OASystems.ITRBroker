using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NCrontab;

namespace OASystems.ITRBroker
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly string _message;
        private readonly CrontabSchedule _schedule;
        private DateTime _nextRun;

        public Worker(ILogger<Worker> logger, string message, string schedule)
        {
            _logger = logger;
            _message = message;

            _schedule = CrontabSchedule.Parse(schedule, new CrontabSchedule.ParseOptions { IncludingSeconds = true });
            _nextRun = _schedule.GetNextOccurrence(DateTime.Now);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (DateTime.Now > _nextRun)
                {
                    _logger.LogInformation("Worker running at: {time}, Message: {test}", DateTimeOffset.Now, _message);
                    _nextRun = _schedule.GetNextOccurrence(DateTime.Now);
                    TestAPI.DoTest(_message);

                }
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}
