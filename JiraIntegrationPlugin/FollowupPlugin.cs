//Librerías
using System;
using System.Net.Http;
using System.Text;
using Microsoft.Xrm.Sdk;
using Newtonsoft.Json;

namespace JiraIntegrationPlugin  //Nombre del plugin/proyecto
{
    public class FollowupPlugin : IPlugin 
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            // Obtener el contexto del servicio y el registro actual
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));

            // Verificar si el mensaje de ejecución es Create y la entidad es "cedi_jiraintegracion"
            if (context.MessageName.ToLower() == "create" && context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity)
            {
                Entity targetEntity = (Entity)context.InputParameters["Target"];

                // Verificar que la entidad es "cedi_jiraintegracion"
                if (targetEntity.LogicalName.ToLower() == "cedi_jiraintegracion")
                {
                    // Llamada a la API de Jira para crear un issue. La contraseña es una API Token generada en Jira
                    string jiraApiUrl = "https://interbanking-sandbox-856.atlassian.net/rest/api/3/issue";
                    string jiraUsername = "rodrigo.bustos@cedi.com.ar";
                    string jiraPassword = "ATATT3xFfGF0ycELOomr3PqKRlEVK7hJM5_ABA5wMqd_8jidIo-A51XlpdCA3jE5grkm6PYgs_FUKFMAyB5NOo8WqMHK5mZ1qdCxrQ3VPRxDPniHGl-cyuu3kJizXzREqvuMpGr3_GghVP7-lAj8FeghyURfSN0yiWsH_gBzUX_Y9BYPSmgHquE=9DF1D71B";

                    CreateJiraIssue(jiraApiUrl, jiraUsername, jiraPassword, targetEntity);

                    // Obtener el servicio de organización para actualizar el registro en Dataverse
                    IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                    IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

                    // Actualizar el registro en Dataverse
                    service.Update(targetEntity);
                }
            }
        }

        private void CreateJiraIssue(string apiUrl, string jiraUsername, string jiraPassword, Entity targetEntity)
        {
            try
            {
                // Obtener los campos del registro
                string ResumenJira = targetEntity.GetAttributeValue<string>("cedi_resumen");
                string DescripcionCRM = targetEntity.GetAttributeValue<string>("cedi_descripcion");
                string KeyProject = targetEntity.GetAttributeValue<string>("cedi_claveproyecto");
                string urlCase = targetEntity.GetAttributeValue<string>("cedi_url_caso");

                // El campo descripción en Jira, cuenta con un texto plano y un texto hipervínculo que redirige al caso origen en CRM
                var descriptionCase = new Content
                {
                    type = "doc",
                    version = 1,
                    content = new System.Collections.Generic.List<Content>
                    {
                        new Content
                        {
                            type = "paragraph",
                            content = new System.Collections.Generic.List<Content>
                            {

                                new Content
                                {          
                                    text = "CRM: " + DescripcionCRM,    //Texto plano al inicio del campo "Descripción" en Jira
                                    type = "text"
                                },
                                new Content
                                {
                                    text = Environment.NewLine + Environment.NewLine + "Click aquí para ver el caso en CRM", //Texto hipervínculo
                                    type = "text",
                                    marks = new System.Collections.Generic.List<Mark>
                                    {
                                        new Mark
                                        {
                                            type = "link",
                                            attrs = new LinkAttributes
                                            {
                                                href = urlCase // Vínculo del caso donde se redirige al clickear
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                };


                // Configurar la autenticación básica para Jira
                var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{jiraUsername}:{jiraPassword}"));

                using (HttpClient client = new HttpClient())
                {
                    // Configurar la autenticación básica para Jira
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);

                    // Establece los datos obtenidos de CRM a Jira
                    var data = new
                    {
                        fields = new
                        {
                            project = new { key = KeyProject },
                            summary = ResumenJira,
                            description =  descriptionCase,
                            issuetype = new { name = "Error" }
                        }
                    };

                    // Convertir a JSON y realizar la llamada POST a la API de Jira
                    string jsonBody = Newtonsoft.Json.JsonConvert.SerializeObject(data, new Newtonsoft.Json.JsonSerializerSettings
                    {
                        NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore //Ignora los campos vacíos
                    });
                    HttpContent content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                    // Realizar la llamada POST a la API de Jira
                    HttpResponseMessage response = client.PostAsync(apiUrl, content).Result;

                    // Puedes manejar la respuesta de Jira aquí (por ejemplo, verificar si la llamada fue exitosa)
                    if (response.IsSuccessStatusCode)
                    {
                        // Procesar la respuesta exitosa
                        string jsonResponse = response.Content.ReadAsStringAsync().Result;

                        // Analizar el JSON para obtener el ID de la issue
                        var responseObject = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(jsonResponse);

                        // Obtener el ID de la issue
                        string issueKey = responseObject.key;

                        // Actualizar el campo "cedi_idissuejira" en el registro actual en Dataverse
                        targetEntity.Attributes["cedi_name"] = issueKey;


                    }
                    else
                    {
                        // Manejar el caso en el que la llamada no fue exitosa
                    }
                }
            }
            catch (Exception ex)
            {
                // Manejar cualquier excepción que pueda ocurrir durante la llamada
            }
        }

        // Clases de apoyo para estructurar la descripción
        public class Content
        {
            public string type { get; set; }
            public int? version { get; set; }
            public System.Collections.Generic.List<Content> content { get; set; }
            public string text { get; set; }
            public System.Collections.Generic.List<Mark> marks { get; set; }
        }

        public class Mark
        {
            public string type { get; set; }
            public LinkAttributes attrs { get; set; }
        }

        public class LinkAttributes
        {
            public string href { get; set; }
        }
    }
}