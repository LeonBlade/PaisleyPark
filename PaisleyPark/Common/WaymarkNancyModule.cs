using Nancy;
using Nancy.ModelBinding;
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
                var waymarks = this.Bind<RESTWaymark>();
                var e = MainWindowViewModel.EventAggregator.GetEvent<WaymarkEvent>();
                e.Publish(waymarks);

                return "OK";
            };
        }
    }
}
