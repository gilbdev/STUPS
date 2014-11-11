﻿/*
 * Created by SharpDevelop.
 * User: Alexander Petrovskiy
 * Date: 7/17/2014
 * Time: 8:06 PM
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */

namespace Tmx.Server.Modules
{
	using System;
	using System.Linq;
	using Nancy;
	using Nancy.ModelBinding;
	using Nancy.Responses.Negotiation;
	using Tmx.Core;
    using Tmx.Core.Types.Remoting;
	using Tmx.Interfaces.Server;
	using Tmx.Interfaces.Remoting;
	
	/// <summary>
	/// Description of TestClientsModule.
	/// </summary>
	public class TestClientsModule : NancyModule
	{
		public TestClientsModule() : base(UrnList.TestClients_Root)
		{
			Post[UrnList.TestClientRegistrationPoint_relPath] = _ => createNewClient(this.Bind<TestClient>());
			
			Delete[UrnList.TestClientDeregistrationPoint_relPath] = parameters => deleteClientById(parameters.id);
			
			Put[UrnList.TestClient_Status_relPath] = parameters => {
            	var detailedStatus = this.Bind<DetailedStatus>();
                return updateStatus(parameters.id, detailedStatus);
			};
		    
		    Get[UrnList.TestClientRegistrationPoint_relPath] = _ => returnAllClients();
		    
		    Get[UrnList.TestClientQueryPoint_relPath] = parameters => returnClientById(parameters.id);
		}
		
		Negotiator createNewClient(TestClient testClient)
		{
		    if (!TestRunQueue.TestRuns.HasActiveTestRuns())
		        return Negotiate.WithStatusCode(HttpStatusCode.ExpectationFailed);
			testClient.TestRunId = TestRunQueue.TestRuns.ActiveTestRunIds().First();
			ClientsCollection.Clients.Add(testClient);
			// TODO: DI
			var taskSorter = new TaskSelector();
			var tasksForClient = taskSorter.SelectTasksForClient(testClient.Id, TaskPool.Tasks);
			tasksForClient.ForEach(task => task.TestRunId = testClient.TestRunId);
			TaskPool.TasksForClients.AddRange(tasksForClient);
			return Negotiate.WithModel(testClient).WithStatusCode(HttpStatusCode.Created);
		}
		
		HttpStatusCode deleteClientById(Guid clientId)
		{
			ClientsCollection.Clients.RemoveAll(client => client.Id == clientId);
			TaskPool.TasksForClients.Where(task => task.ClientId == clientId && task.TaskStatus == TestTaskStatuses.New)
			    .ToList()
			    .ForEach(task => task.TaskStatus = TestTaskStatuses.Canceled);
			return HttpStatusCode.NoContent;
		}
		
        HttpStatusCode updateStatus(Guid clientId, DetailedStatus detailedStatus)
        {
            if (ClientsCollection.Clients.All(client => client.Id != clientId))
                return HttpStatusCode.NotFound;
            ClientsCollection.Clients.First(client => client.Id == clientId).DetailedStatus = detailedStatus.Status;
            return HttpStatusCode.OK;
        }
        
		Negotiator returnAllClients()
		{
			return 0 == ClientsCollection.Clients.Count ? Negotiate.WithStatusCode(HttpStatusCode.NotFound) : Negotiate.WithModel(ClientsCollection.Clients).WithStatusCode(HttpStatusCode.OK);
		}
		
		Negotiator returnClientById(Guid clientId)
		{
			// TODO: refactor this
		    return ClientsCollection.Clients.Any(client => client.Id == clientId) ? Negotiate.WithModel(ClientsCollection.Clients.First(client => client.Id == clientId)).WithStatusCode(HttpStatusCode.OK) : Negotiate.WithStatusCode(HttpStatusCode.NotFound);
		}
	}
}
