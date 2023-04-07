/*
****************************************************************************
*  Copyright (c) 2023,  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

By using this script, you expressly agree with the usage terms and
conditions set out below.
This script and all related materials are protected by copyrights and
other intellectual property rights that exclusively belong
to Skyline Communications.

A user license granted for this script is strictly for personal use only.
This script may not be used in any way by anyone without the prior
written consent of Skyline Communications. Any sublicensing of this
script is forbidden.

Any modifications to this script by the user are only allowed for
personal use and within the intended purpose of the script,
and will remain the sole responsibility of the user.
Skyline Communications will not be responsible for any damages or
malfunctions whatsoever of the script resulting from a modification
or adaptation by the user.

The content of this script is confidential information.
The user hereby agrees to keep this confidential information strictly
secret and confidential and not to disclose or reveal it, in whole
or in part, directly or indirectly to any person, entity, organization
or administration without the prior written consent of
Skyline Communications.

Any inquiries can be addressed to:

	Skyline Communications NV
	Ambachtenstraat 33
	B-8870 Izegem
	Belgium
	Tel.	: +32 51 31 35 69
	Fax.	: +32 51 31 01 29
	E-mail	: info@skyline.behy
	Web		: www.skyline.be
	Contact	: Ben Vandenberghe

****************************************************************************
Revision History:

DATE		VERSION		AUTHOR			COMMENTS

23/03/2023	1.0.0.1		JDE, Skyline	Initial version
****************************************************************************
*/
namespace Enable_Disable_ScheduledTasks_1
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages;
	using Skyline.DataMiner.Net.Messages.Advanced;

	/// <summary>
	///     DataMiner Script Class.
	/// </summary>
	public class Script
	{
		/// <summary>
		///     The Script entry point.
		/// </summary>
		/// <param name="engine">Link with SLAutomation process.</param>
		public void Run(Engine engine)
		{
			// Retrieve script input parameters
			Input input = GetInput(engine);

			// Retrieve Scheduled task on this agent
			var schedulerTask = GetTask(engine, input);

			if (schedulerTask.Enabled == input.Enable)
			{
				var msg = $"Scheduled task {input.Name} already has the desired state: IsEnabled = {input.Enable}";
				engine.GenerateInformation(msg);
				engine.ExitSuccess(msg);
			}

			// Send update
			UpdateTask(engine, input, schedulerTask);
		}

		private static SA[] GetActions(SchedulerAction[] actions)
		{
			return actions.Select(x => new SA(GetScriptActions(x))).ToArray();
		}

		private static string[] GetScriptActions(SchedulerAction action)
		{
			if (action.ActionType == SchedulerActionType.Automation)
			{
				List<string> options = new List<string> { "automation", action.ScriptInstance.ScriptName };
				foreach (object arrayItem in action.ScriptInstance.ProtocolIdToElementId)
				{
					var info = (AutomationScriptInstanceInfo)arrayItem;
					options.Add($"PROTOCOL:{info.Key}:{info.Value.Replace("/", ":")}");
				}

				foreach (object arrayItem in action.ScriptInstance.ParameterIdToValue)
				{
					var info = (AutomationScriptInstanceInfo)arrayItem;
					options.Add($"PARAMETER:{info.Key}:{info.Value}");
				}

				options.Add("CHECKSETS:" + (action.ScriptInstance.CheckSets ? "TRUE" : "FALSE"));
				options.Add("DEFER:FALSE");

				return options.ToArray();
			}

			if (action.ActionType == SchedulerActionType.Information)
			{
				return new string[] { "information", action.Message };
			}

			if (action.ActionType == SchedulerActionType.Notification)
			{
				List<string> options = new List<string>();
				if (!String.IsNullOrEmpty(action.MailReportTemplate))
				{
					options.Add("report");
					options.Add(action.MailReportTemplate);
					options.Add(action.EmailSubj);
					options.Add(action.Destination);
					options.Add(action.EmailCC);
					options.Add(action.EmailBCC);
					options.Add(action.Message);
					options.Add(action.MailReportTemplates[0].ElementID);
				}
				else
				{
					options.Add("notification");
					options.Add(action.Message);
					options.Add("email");
					options.Add(action.Destination);
					options.Add(action.EmailSubj);
					options.Add(action.EmailCC);
					options.Add(action.EmailBCC);
				}

				return options.ToArray();
			}

			throw new InvalidOperationException($"Scheduled Action type {action.ActionType} not supported yet.");
		}

		private static SchedulerTask GetTask(Engine engine, Input input)
		{
			var responseMessage = engine.SendSLNetSingleResponseMessage(new GetInfoMessage(InfoType.SchedulerTasks)) as GetSchedulerTasksResponseMessage;
			if (responseMessage == null)
			{
				throw new InvalidOperationException("Could not retrieve the scheduled task information from the system.");
			}

			SchedulerTask task;
			foreach (var arrayItemTask in responseMessage.Tasks)
			{
				task = (SchedulerTask)arrayItemTask;
				if (task.TaskName == input.Name)
				{
					return task;
				}
			}

			throw new InvalidOperationException($"Scheduled task {input.Name} does not exist.");
		}

		private static void UpdateTask(Engine engine, Input input, SchedulerTask schedulerTask)
		{
			// Build update data
			var setSchedulerInfoMessage = new SetSchedulerInfoMessage
			{
				DataMinerID = schedulerTask.HandlingDMA,
				HostingDataMinerID = schedulerTask.HandlingDMA,
				Info = Int32.MaxValue,
				What = 2,
				Ppsa = new PPSA
				{
					Ppsa = new PSA[]
					{
						new PSA
						{
							Psa = new SA[]
							{
								new SA(
									new string[]
									{
										schedulerTask.Id.ToString(), // [0] : task ID
										schedulerTask.TaskName, ////taskName, // [1] : name
										schedulerTask.StartTime.Date.ToString("yyyy-MM-dd"), ////activStartDay, // [2] : start date (YYYY-MM-DD) (leave empty to have start time == current time)
										schedulerTask.EndTime.Date.ToString("yyyy-MM-dd"), ////activStopDay, // [3] : end date (YYYY-MM-DD)      (can be left empty)
										schedulerTask.StartTime.TimeOfDay.ToString(), ////startTime, // [4] : start run time (HH:MM)
										schedulerTask.RepeatType.ToString().ToLower(), ////taskType, // [5] : task type     (daily   / monthly            / weekly /                      once)
										schedulerTask.RepeatInterval, ////runInterval, // [6] : run interval  (x minutes / 1,...,31,101,102   / 1,3,5,7 (1=monday, 7=sunday)) (101-112 -> months)
										schedulerTask.Repeat.ToString(), ////"", // [7] : # of repeats before final actions are executed
										schedulerTask.Description, ////taskDescription, // [8] : description
										input.Enable.ToString().ToUpper(), // [9] : enabled (TRUE/FALSE)
										schedulerTask.EndTime.TimeOfDay.ToString(), ////endTime, // [10] : end run time (HH:MM) (only affects daily tasks)
										schedulerTask.EndTime.ToString(), ////"", // [11]: minutes interval for weekly/monthly tasks either an enddate or a repeat count should be specified
									}),
							},
						},
						new PSA
						{
							Psa = GetActions(schedulerTask.Actions),
						},
						new PSA
						{
							Psa = GetActions(schedulerTask.FinalActions),
						},
					},
				},
			};

			// send update command
			engine.SendSLNetSingleResponseMessage(setSchedulerInfoMessage);
			engine.GenerateInformation($"Scheduled task {input.Name} has been {(input.Enable ? "ENABLED" : "DISABLED")}");
		}

		private Input GetInput(Engine engine)
		{
			string taskName = engine.GetScriptParam("Name").Value;
			string enableRaw = engine.GetScriptParam("Status(Enable/Disable)").Value;

			if (!enableRaw.Equals("Disable") && !enableRaw.Equals("Enable"))
			{
				throw new InvalidOperationException("Expected the value 'Enable' or 'Disable' as input to the Status parameter.");
			}

			return new Input
			{
				Enable = enableRaw.Equals("Enable"),
				Name = taskName,
			};
		}
	}

	public sealed class Input
	{
		public bool Enable { get; set; }

		public string Name { get; set; }
	}
}