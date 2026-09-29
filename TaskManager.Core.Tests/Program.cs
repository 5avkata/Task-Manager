using TaskManager.Models;
using TaskManager.Services;

var failures = new List<string>();
var checks = 0;
void Check(bool value, string name) { checks++; if (!value) failures.Add(name); }
var now = new DateTime(2026, 9, 28, 14, 30, 0);
var data = new AppData
{
    Categories = ["Personal", "School", "Work", "Fitness"], Tags = ["Exam", "Focus"],
    Tasks =
    [
        new() { Title="Review algebra", Description="Practice problems", Notes="Use the formula sheet", Category="School", Priority=TaskPriority.High, DueDate=now.Date, Tags=["Exam"], Subtasks=[new(){Title="Chapter 1",IsCompleted=true},new(){Title="Chapter 2"}] },
        new() { Title="Monthly review", Category="Work", DueDate=new DateTime(2026,1,31), Recurrence=RecurrenceType.Monthly, ReminderAt=new DateTime(2026,1,31,9,0,0) },
        new() { Title="Old task", Category="Personal", DueDate=now.Date.AddDays(-2) }
    ]
};
Check(TaskQuery.Filter(data.Tasks,TaskView.Today,"",null,null,TaskSort.DueDate,now.Date).Count==1,"Today query");
Check(TaskQuery.Filter(data.Tasks,TaskView.AllTasks,"formula",null,null,TaskSort.DueDate,now.Date).Count==1,"Notes search");
Check(TaskQuery.Filter(data.Tasks,TaskView.AllTasks,"",null,null,TaskSort.DueDate,now.Date,"Exam").Count==1,"Tag filter");
Check(TaskQuery.Filter(data.Tasks,TaskView.AllTasks,"",null,null,TaskSort.Priority,now.Date)[0].Priority==TaskPriority.High,"Priority sort");
Check(data.Tasks.Single(t=>t.Title=="Old task").IsOverdue(now.Date),"Overdue state");
Check(data.Tasks[0].SubtaskPercent==50,"Subtask progress");
var next=TaskLogic.Complete(data,data.Tasks[1].Id,now);
Check(next?.DueDate==new DateTime(2026,2,28),"Safe monthly recurrence");
Check(next?.ReminderAt==new DateTime(2026,2,28,9,0,0),"Recurring reminder offset");
Check(TaskLogic.Complete(data,data.Tasks[1].Id,now.AddMinutes(1)) is null,"Recurrence duplicate prevention");
TaskLogic.Complete(data,data.Tasks[0].Id,now);
Check(data.Tasks[0].CompletedAt==now,"Completion timestamp");
TaskLogic.MarkIncomplete(data.Tasks[0]);
Check(!data.Tasks[0].IsCompleted&&data.Tasks[0].CompletedAt is null,"Mark incomplete");
var reminder=next!; reminder.ReminderAt=now.AddMinutes(-1);
Check(TaskLogic.DueReminders(data,now).Single().Id==reminder.Id,"Due reminder");
reminder.ReminderTriggered=true;
Check(TaskLogic.DueReminders(data,now).Count==0,"One-time reminder");
var json=TaskStore.Serialize(data);var roundTrip=TaskStore.Parse(json);
Check(roundTrip.Tasks.Count==data.Tasks.Count&&roundTrip.Tags.SequenceEqual(data.Tags),"Expanded JSON round trip");
var old=TaskStore.Parse("{\"Version\":1,\"Categories\":[\"Personal\",\"School\",\"Work\"],\"Tasks\":[{\"Title\":\"Legacy\",\"Category\":\"Personal\",\"Priority\":\"Low\",\"DueDate\":\"2026-01-01\"}]}");
Check(old.Tasks[0].Tags.Count==0&&old.Tasks[0].Subtasks.Count==0,"Legacy JSON defaults");
var copy=data.Copy();copy.Tasks[0].Subtasks[0].Title="Changed";
Check(data.Tasks[0].Subtasks[0].Title=="Chapter 1","Deep copy");
var insights=TaskLogic.Insights(data.Tasks,now);
Check(insights.Overdue==2&&insights.Total==4,"Insights");
var directory=Path.Combine(Path.GetTempPath(),"TaskManager-CoreTests-"+Guid.NewGuid().ToString("N"));
try
{
    var store=new TaskStore(directory);store.Save(data);Check(store.Load().Data.Tasks.Count==4,"Store save/load");
    var export=Path.Combine(directory,"export.json");TaskStore.Export(export,data);Check(TaskStore.Import(export).Tasks.Count==4,"Backup export/import");
    File.WriteAllText(export,"{bad");bool rejected=false;try{TaskStore.Import(export);}catch(System.Text.Json.JsonException){rejected=true;}Check(rejected,"Invalid import rejected");
}
finally{try{Directory.Delete(directory,true);}catch{}}
if(failures.Count>0){Console.Error.WriteLine("FAILED: "+string.Join(", ",failures));return 1;}
Console.WriteLine($"SUCCESS: {checks} core checks passed.");return 0;
