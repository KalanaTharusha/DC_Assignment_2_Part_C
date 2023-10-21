using Client_DLL;
using IronPython.Hosting;
using Job_DLL;
using Microsoft.Scripting.Hosting;
using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Text;
using System.Security.Cryptography;
using System.IO;

namespace Client_Desktop_Application
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		ServiceHost host;
		List<Client> clients = new List<Client> ();
		private IJobServer foob;
		private int port;
		private int clientID;
		private int totalJobsCompleted = 0;
		private string currJobProgress = "Idle";

		public MainWindow()
		{
			InitializeComponent();

		}

		private void Server()
		{

			NetTcpBinding tcp = new NetTcpBinding();

			host = new ServiceHost(typeof(JobServer));

			host.AddServiceEndpoint(typeof(IJobServer), tcp, "net.tcp://localhost:" + port + "/JobService");
			host.Open();
			Console.WriteLine("System Online");
			Console.ReadLine();
			//host.Close();
		}

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
				try
				{
					foreach (var client in clients)
					{
						if (client.Port != port)
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

									Thread.Sleep(2000);

									foob.SubmitResult(job.JobId, sResult);
									currJobProgress = "Completed";
									UpdateClient();

									Dispatcher.Invoke(() =>
									{
										ProgressLbl.Foreground = Brushes.Green;
										ProgressLbl.Content = currJobProgress;
									});

									Thread.Sleep(2000);

									totalJobsCompleted++;
									currJobProgress = "Idle";
									UpdateClient();
								}
							}
						}
						Dispatcher.Invoke(() =>
						{
							ProgressLbl.Foreground = Brushes.Blue;
							ProgressLbl.Content = currJobProgress;
						});
					}

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

					restResponse = restClient.Execute(restRequest);
					clients = JsonConvert.DeserializeObject<IEnumerable<Client>>(restResponse.Content);
				} 
				catch (Exception ex)
				{
					MessageBox.Show(ex.Message);
				}
			}

		}

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

		private dynamic RunPythonScript(string script)
		{
			ScriptEngine engine = Python.CreateEngine();
			ScriptScope scope = engine.CreateScope();

			engine.Execute(script, scope);

			dynamic result = scope.GetVariable("main");

			return result();
		}

		private void UpdateClient ()
		{
			try
			{
				RestClient restClient = new RestClient("http://localhost:5082");
				RestRequest restRequest;
				RestResponse restResponse;

				restRequest = new RestRequest("/api/clients/" + clientID, Method.Get);
				restResponse = restClient.Execute(restRequest);

				Client client = JsonConvert.DeserializeObject<Client>(restResponse.Content);
				client.JobsCompleted = totalJobsCompleted;
				client.Status = currJobProgress;

				restRequest = new RestRequest("/api/clients/" + clientID, Method.Put);
				restRequest.AddBody(client);

				restResponse = restClient.Execute(restRequest);
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

		private void ExitBtn_Click(object sender, RoutedEventArgs e)
		{
			RestClient restClient = new RestClient("http://localhost:5082");
			RestRequest restRequest = new RestRequest("/api/clients/" + clientID, Method.Delete);
			RestResponse restResponse = restClient.Execute(restRequest);

			host.Close();

			RegPanel.Visibility = Visibility.Visible;
			MainPanel.Visibility = Visibility.Hidden;
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
			else if (!script.Text.Contains("def main():"))
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
	}
}
