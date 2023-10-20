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

namespace Client_Desktop_Application
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
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
            ServiceHost host;

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
                                Dispatcher.Invoke(() =>
                                {
                                    ProgressLbl.Foreground = Brushes.Red;
                                    ProgressLbl.Content = "In progress";
                                });

                                var result = RunPythonScript(job.PythonScript);
                                string sResult = result.ToString();

                                Thread.Sleep(2000);

                                foob.SubmitResult(job.JobId, sResult);

                                Dispatcher.Invoke(() =>
                                {
                                    ProgressLbl.Foreground = Brushes.Green;
                                    ProgressLbl.Content = "Completed";
                                });

                                Thread.Sleep(2000);

                                totalJobsCompleted++;
                            }
                        }
                        Dispatcher.Invoke(() =>
                        {
                            ProgressLbl.Foreground = Brushes.Blue;
                            ProgressLbl.Content = "Idle";
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
            port = int.Parse(PortTB.Text);
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

        private void RegBtn_Click(object sender, RoutedEventArgs e)
        {
            Register();
        }

        private void ExitBtn_Click(object sender, RoutedEventArgs e)
        {
            RestClient restClient = new RestClient("http://localhost:5082");
            RestRequest restRequest = new RestRequest("/api/clients/" + clientID, Method.Delete);
            RestResponse restResponse = restClient.Execute(restRequest);


            RegPanel.Visibility = Visibility.Visible;
            MainPanel.Visibility = Visibility.Hidden;
        }

        private void PostBtn_Click(object sender, RoutedEventArgs e)
        {
            Job job = new Job();
            job.JobId = JobList.Jobs.Count + 1;
            job.Status = Job.JobStatus.ToDo;
            TextRange script = new TextRange(ScriptTB.Document.ContentStart, ScriptTB.Document.ContentEnd);
            job.PythonScript = script.Text;

            JobList.Jobs.Add(job);
            
        }
    }
}
