using CommandTerminal;
using DV.Logic.Job;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DVRouteManager
{
    public class RouteTaskChain
    {
        protected RouteTaskChain()
        {

        }

        public List<RouteTask> tasks;
        public RouteTaskChain nextTasks;

        public static RouteTaskChain FromDVJob(Job job)
        {
            if (job.tasks.Count == 1)
            {
                Task task = job.tasks[0];
                return FromDVTask(task);
            }

            return null;
        }

        public static RouteTaskChain FromDVTask(Task task)
        {
            switch (task.InstanceTaskType)
            {
                case TaskType.Transport:
                case TaskType.Warehouse:
                    return new RouteTaskChain()
                    {
                        tasks = new List<RouteTask>()
                        {
                            RouteTask.FromDVTask(task)
                        }
                    };

                case TaskType.Sequential:
                    RouteTaskChain first = null;
                    RouteTaskChain prev = null;
                    foreach (var nestedTask in task.GetTaskData().nestedTasks)
                    {
                        if (nestedTask.IsTaskCompleted())
                            continue;

                        RouteTaskChain current = null;

                        switch (nestedTask.InstanceTaskType)
                        {
                            case TaskType.Transport:
                            case TaskType.Warehouse:
                            case TaskType.Parallel:
                                current = FromDVTask(nestedTask); break;
                            default:
                                Terminal.Log($"Not supported task type {nestedTask.InstanceTaskType} inside sequence"); break;
                        }

                        if (current != null)
                        {
                            if (prev != null)
                            {
                                prev.nextTasks = current;
                            }

                            prev = current;
                        }

                        if (first == null)
                        {
                            first = current;
                        }
                    }
                    return first;
                case TaskType.Parallel:
                    RouteTaskChain result = new RouteTaskChain();
                    result.tasks = task.GetTaskData().nestedTasks
                        .Where(t => ! t.IsTaskCompleted())
                        .Select(t => RouteTask.FromDVTask(t))
                        .Where(rt => rt != null)
                        .ToList();

                    return result;
            }

            return null;
        }

        public static RouteTaskChain FromDestination(Track destination, Trainset trainset)
        {
            return new RouteTaskChain()
            {
                tasks = new List<RouteTask>()
                {
                    RouteTask.FromDestination(destination, trainset)
                }
            };
        }

        public override string ToString()
        {
            var chain = this;
            StringBuilder sb = new StringBuilder();
            do
            {
                if (chain.tasks != null)
                {
                    chain.tasks.ForEach(t =>
                    {
                        sb.Append($"{t.DestinationTrack?.ID}({t.TrainSets?.Count}); ");
                    });
                }
                sb.Append("-> ");
                chain = chain.nextTasks;
            }
            while (chain != null);

            return sb.ToString();
        }
    }
    public class RouteTask
    {
        public Track DestinationTrack { get; protected set; }
        public Task DVTask { get; protected set; }
        public HashSet<Trainset> TrainSets { get; } = new HashSet<Trainset>();
        protected RouteTask()
        {

        }

        public static RouteTask FromDVTask(Task task)
        {
            RouteTask newTask = new RouteTask();

#if DEBUG
            Terminal.Log($"job {task.Job.ID} type {task.InstanceTaskType} state {task.state}");
#endif

            switch(task.InstanceTaskType)
            {
                case TaskType.Transport:
                    newTask.InitFromTransportTask(task as TransportTask);
                    break;
                case TaskType.Warehouse:
                    newTask.InitFromWarehouseTask(task as WarehouseTask);
                    break;
                default:
                    return null;
            }

            return newTask;
        }

        public static RouteTask FromDestination(Track destination, Trainset trainset)
        {
            RouteTask newTask = new RouteTask();
            newTask.DestinationTrack = destination;
            newTask.TrainSets.Add(trainset);

            return newTask;
        }

        private void GetTrainsets(List<Car> cars)
        {
            if (cars == null)
                return;

            cars.ForEach(car =>
            {
                TrainCar trainCar;
                if (!TrainCar.logicCarToTrainCar.TryGetValue(car, out trainCar))
                    return;

                if (!TrainSets.Contains(trainCar.trainset))
                    TrainSets.Add(trainCar.trainset);
            });
        }

        private void InitFromWarehouseTask(WarehouseTask task)
        {
            Terminal.Log($"warehouse task nested: {task.GetTaskData().nestedTasks?.Count} cars: {task.GetTaskData().cars?.Count}");

            DestinationTrack = task.GetTaskData().destinationTrack;
            DVTask = task;
            GetTrainsets(task.GetTaskData().cars);
        }

        private void InitFromTransportTask(TransportTask task)
        {
            Terminal.Log($"transport task nested: {task.GetTaskData().nestedTasks?.Count} cars: {task.GetTaskData().cars?.Count}");

            DestinationTrack = task.GetTaskData().destinationTrack;
            DVTask = task;
            GetTrainsets(task.GetTaskData().cars);
        }
    }
}
