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
	E-mail	: info@skyline.be
	Web		: www.skyline.be
	Contact	: Ben Vandenberghe

****************************************************************************
Revision History:

DATE		VERSION		AUTHOR			COMMENTS

dd/mm/2023	1.0.0.1		XXX, Skyline	Initial version
****************************************************************************
*/
namespace AutomationTest_2
{
	using System;
	using System.Linq;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Core.DataMinerSystem.Automation;
	using Skyline.DataMiner.Core.DataMinerSystem.Common;

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
			var dms = engine.GetDms();
			var dma = dms.GetAgents().First();
			var schedulerTask = GetTask(dma, input);

			if (schedulerTask.IsEnabled == input.Enable)
			{
				var msg = $"Scheduled task {input.Name} already has the desired state: IsEnabled = {input.Enable}";
				engine.GenerateInformation(msg);
				engine.ExitSuccess(msg);
			}

			// Send update
			UpdateTask(engine, dma, input, schedulerTask);
		}

		private static IDmsSchedulerTask GetTask(IDma dma, Input input)
		{
			var task = dma.Scheduler.GetTasks().FirstOrDefault(x => x.TaskName == input.Name);
			if (task == null)
			{
				throw new InvalidOperationException($"Scheduled task {input.Name} does not exist.");
			}

			return task;
		}

		private static void UpdateTask(Engine engine, IDma dma, Input input, IDmsSchedulerTask schedulerTask)
		{
			// Build update data
			object[] updateData = new object[]
			{
				new object[]
				{
					new string[] // general info
					{
						schedulerTask.Id.ToString(), // [0] : task ID
						null, ////taskName, // [1] : name
						null, ////activStartDay, // [2] : start date (YYYY-MM-DD) (leave empty to have start time == current time)
						null, ////activStopDay, // [3] : end date (YYYY-MM-DD)      (can be left empty)
						null, ////startTime, // [4] : start run time (HH:MM)
						null, ////taskType, // [5] : task type     (daily   / monthly            / weekly /                      once)
						null, ////runInterval, // [6] : run interval  (x minutes / 1,...,31,101,102   / 1,3,5,7 (1=monday, 7=sunday)) (101-112 -> months)
						null, ////"", // [7] : # of repeats before final actions are executed
						null, ////taskDescription, // [8] : description
						input.Enable ? "TRUE" : "FALSE", // [9] : enabled (TRUE/FALSE)
						null, ////endTime, // [10] : end run time (HH:MM) (only affects daily tasks)       
						null, ////"", // [11]: minutes interval for weekly/monthly tasks either an enddate or a repeat count should be specified
					},
				},

				new object[] // repeat actions
				{
					//new string[]
					//{
					//	"automation",           // action type 
					//	scriptName,             // name of automation script
					//	elemLinked,             // example of linking element 123/456 to script dummy 1
					//	paramLinked,            // ... other options & further linking of dummies to elements can be added
					//	// elem2Linked,
					//	"CHECKSETS:FALSE",
					//	"DEFER:False",			// run sync
					//}
				},
				new object[] { }, // final actions
			};

			// send update command
			dma.Scheduler.UpdateTask(updateData);
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