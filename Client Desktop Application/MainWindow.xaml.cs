using Client_DLL;
using IronPython.Hosting;
using Job_DLL;
using Microsoft.Scripting.Hosting;
using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Text;
using System.Security.Cryptography;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace Client_Desktop_Application
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		ServiceHost host;
		private IJobServer foob;
		private int port;
		private int clientID;
		private int totalJobsCompleted = 0;
		private string currJobProgress = "Idle";
		private bool isClosed = false;

		public MainWindow()
		{
			InitializeComponent();

		}

		// Function for Server thread to host .NET Remoting service
		private void Server()
		{

			NetTcpBinding tcp = new NetTcpBinding();

			host = new ServiceHost(typeof(JobServer));

			host.AddServiceEndpoint(typeof(IJobServer), tcp, "net.tcp://localhost:" + port + "/JobService");
			host.Open();
			Console.WriteLine("System Online");
			Console.ReadLine();

			while (!isClosed)
			{
				
			}
			host.Close();
		}

		// Function for Network thread to look for new clients and check for jobs to do
		private void Network()
		{
			RestClient restClient = new RestClient("http://localhost:5082");
			RestRequest restRequest;
			RestResponse restResponse;

			restRequest = new RestRequest("/api/clients", Method.Get);
			restResponse = restClient.Execute(restRequest);

			IEnumerable<Client> clients = JsonConvert.DeserializeObject<IEnumerable<Client>>(restResponse.Content);
			
			while(true)
			{
				foreach (var client in clients)
				{
					if (client.Port != port)
					{
						try
						{
							ChannelFactory<IJobServer> foobFactory;
							NetTcpBinding tcp = new NetTcpBinding();

							string URL = "net.tcp://localhost:" + client.Port + "/JobService";
							foobFactory = new ChannelFactory<IJobServer>(tcp, URL);
							foob = foobFactory.CreateChannel();

							Job job = foob.RequestJob();

							if (job != null)
							{
								currJobProgress = "In Progress";
								UpdateClient();

								Dispatcher.Invoke(() =>
								{
									ProgressLbl.Foreground = Brushes.Red;
									ProgressLbl.Content = currJobProgress;
								});

								byte[] encodedString = Convert.FromBase64String(job.PythonScript);

								SHA256 sha256hASH = SHA256.Create();
								byte[] hash = sha256hASH.ComputeHash(encodedString);

								if (hash.SequenceEqual(job.Hash))
								{
									String pythonScript = Encoding.UTF8.GetString(encodedString);

									var result = RunPythonScript(pythonScript);
									string sResult = result.ToString();

									Thread.Sleep(1000);

									foob.SubmitResult(job.JobId, sResult);
									currJobProgress = "Completed";
									UpdateClient();

									Dispatcher.Invoke(() =>
									{
										ProgressLbl.Foreground = Brushes.Green;
										ProgressLbl.Content = currJobProgress;
									});

									Thread.Sleep(1000);

									totalJobsCompleted++;
									currJobProgress = "Idle";
									UpdateClient();
								}
							}
							Dispatcher.Invoke(() =>
							{
								ProgressLbl.Foreground = Brushes.Blue;
								ProgressLbl.Content = currJobProgress;
							});
						}
						catch (TaskCanceledException e)
						{
							restResponse = restClient.Execute(restRequest);
							clients = JsonConvert.DeserializeObject<IEnumerable<Client>>(restResponse.Content);
						}
						catch (Exception e)
						{
							MessageBox.Show(e.Message);
							restResponse = restClient.Execute(restRequest);
							clients = JsonConvert.DeserializeObject<IEnumerable<Client>>(restResponse.Content);
						}
					}
				}

				try
				{
					Dispatcher.Invoke(() =>
					{
						foreach (var job in JobList.Jobs)
						{
							if (job.Status.Equals(Job.JobStatus.Completed))
							{
								job.Status = Job.JobStatus.Displayed;
								Paragraph paragraph = new Paragraph(new Run(job.Result));
								ResultTB.Document.Blocks.Add(paragraph);
							}
						}

						TotalLbl.Content = totalJobsCompleted;
					});
				}
				catch (TaskCanceledException)
				{
					restResponse = restClient.Execute(restRequest);
					clients = JsonConvert.DeserializeObject<IEnumerable<Client>>(restResponse.Content);
				}
				catch (Exception e)
				{
					MessageBox.Show(e.Message);
					restResponse = restClient.Execute(restRequest);
					clients = JsonConvert.DeserializeObject<IEnumerable<Client>>(restResponse.Content);
				}
				

				restResponse = restClient.Execute(restRequest);
				clients = JsonConvert.DeserializeObject<IEnumerable<Client>>(restResponse.Content);
			}

		}

		// New client register function
		private void Register()
		{
			if (!int.TryParse(PortTB.Text, out port))
			{
				MessageBox.Show("Invalid Port");
				return;
			}

			port = int.Parse(PortTB.Text);

			if (!(port >= 0 && port <= 65535))
			{
				MessageBox.Show("Invalid Port");
				return;
			}

			RestClient restClient = new RestClient("http://localhost:5082");
			RestRequest restRequest;
			RestResponse restResponse;

			restRequest = new RestRequest("/api/clients", Method.Get);
			restResponse = restClient.Execute(restRequest);

			if (restResponse.StatusCode != HttpStatusCode.OK)
			{
				MessageBox.Show("Cannot connect to the Database!");
				return;
			}

			IEnumerable<Client> clients = JsonConvert.DeserializeObject<IEnumerable<Client>>(restResponse.Content);

			if (!clients.Any(c => c.Port == port))
			{
				restRequest = new RestRequest("/api/clients", Method.Post);

				Client client = new Client();
				client.IPAddress = "127.0.0.1";
				client.Port = port;
				client.Status = "Idle";
				client.JobsCompleted = 0;
				restRequest.AddBody(client);

				restResponse = restClient.Execute(restRequest);

				if (restResponse.StatusCode != HttpStatusCode.Created)
				{
					MessageBox.Show("Cannot connect to the Database!");
					return;
				}

				clientID = JsonConvert.DeserializeObject<Client>(restResponse.Content).ClientId;

				Thread serverThread = new Thread(new ThreadStart(Server));
				serverThread.Start();

				Thread networkingThread = new Thread(new ThreadStart(Network));
				networkingThread.Start();

				RegPanel.Visibility = Visibility.Hidden;
				MainPanel.Visibility = Visibility.Visible;

				Title = port.ToString();

			}
			else
			{
				MessageBox.Show("Port not available");
			}
		}

		// Function to run Python scripts
		private dynamic RunPythonScript(string script)
		{
			ScriptEngine engine = Python.CreateEngine();
			ScriptScope scope = engine.CreateScope();

			engine.Execute(script, scope);

			dynamic result = scope.GetVariable("main");

			return result();
		}

		// Function to update client status in database
		private void UpdateClient ()
		{
			try
			{
				RestClient restClient = new RestClient("http://localhost:5082");
				RestRequest restRequest;
				RestResponse restResponse;

				restRequest = new RestRequest("/api/clients/" + clientID, Method.Get);
				restResponse = restClient.Execute(restRequest);

				if (restResponse.StatusCode != HttpStatusCode.OK)
				{
					MessageBox.Show("Cannot connect to the Database!");
					return;
				}

				Client client = JsonConvert.DeserializeObject<Client>(restResponse.Content);
				client.JobsCompleted = totalJobsCompleted;
				client.Status = currJobProgress;

				restRequest = new RestRequest("/api/clients/" + clientID, Method.Put);
				restRequest.AddBody(client);

				restResponse = restClient.Execute(restRequest);

				if (restResponse.StatusCode != HttpStatusCode.NoContent)
				{
					MessageBox.Show("Cannot update the client!");
					return;
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message);
			}
		}
		private void RegBtn_Click(object sender, RoutedEventArgs e)
		{
			Register();
		}

		private void PostBtn_Click(object sender, RoutedEventArgs e)
		{
			Job job = new Job();
			job.JobId = JobList.Jobs.Count + 1;
			job.Status = Job.JobStatus.ToDo;

			TextRange script = new TextRange(ScriptTB.Document.ContentStart, ScriptTB.Document.ContentEnd);

			if (string.IsNullOrWhiteSpace(script.Text))
			{
				MessageBox.Show("Enter a Python script!");
				return;
			}
			if (!script.Text.Contains("def main():"))
			{
				MessageBox.Show("Cannot find the main method!");
				return;
			}

			byte[] textBytes = Encoding.UTF8.GetBytes(script.Text);
			job.PythonScript = Convert.ToBase64String(textBytes);

			SHA256 sha256hASH = SHA256.Create();
			byte[] hash = sha256hASH.ComputeHash(textBytes);
			job.Hash = hash;

			JobList.Jobs.Add(job);
			
		}

		private void UploadBtn_Click(object sender, RoutedEventArgs e)
		{
			Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
			openFileDialog.Filter = "Python Files (*.py)|*.py";

			if (openFileDialog.ShowDialog() == true)
			{
				string filePath = openFileDialog.FileName;
				string pythonCode = File.ReadAllText(filePath);

				Dispatcher.Invoke(() =>
				{
					ScriptTB.Document.Blocks.Clear();
					ScriptTB.AppendText(pythonCode);
				});
			}
		}

		private void MainWindow_OnClosing(object sender, CancelEventArgs e)
		{
			RestClient restClient = new RestClient("http://localhost:5082");
			RestRequest restRequest = new RestRequest("/api/clients/" + clientID, Method.Delete);
			RestResponse restResponse = restClient.Execute(restRequest);

			if (restResponse.StatusCode != HttpStatusCode.NoContent)
			{
				isClosed = true;
				MessageBox.Show("Cannot remove the client!");
			}

		}
	}
}
