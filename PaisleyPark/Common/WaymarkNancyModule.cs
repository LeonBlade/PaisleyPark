using Nancy;
using Nancy.ModelBinding;
using Newtonsoft.Json;
using PaisleyPark.Models;
using PaisleyPark.ViewModels;

namespace PaisleyPark.Common
{
    public class RESTWaymark
    {
        public Waymark A { get; set; }
        public Waymark B { get; set; }
        public Waymark C { get; set; }
        public Waymark D { get; set; }
        public Waymark One { get; set; }
        public Waymark Two { get; set; }
    }

    public class WaymarkNancyModule : NancyModule
    {
        public WaymarkNancyModule()
        {
            Post["/place"] = data =>
            {
                // Bind the param to RESTWaymark.
                var waymarks = this.Bind<RESTWaymark>();
                // Get event to publish to.
                var e = MainWindowViewModel.EventAggregator.GetEvent<WaymarkEvent>();
                // Publish the waymarks from the request.
                e.Publish(waymarks);

                // Create response.
                var response = new RESTWaymark
                {
                    A = MainWindowViewModel.GameMemory.A,
                    B = MainWindowViewModel.GameMemory.B,
                    C = MainWindowViewModel.GameMemory.C,
                    D = MainWindowViewModel.GameMemory.D,
                    One = MainWindowViewModel.GameMemory.One,
                    Two = MainWindowViewModel.GameMemory.Two
                };

                // Serialize and return the response.
                return JsonConvert.SerializeObject(response);
            };
        }
    }
}
