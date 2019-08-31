using Prism.Events;

namespace PaisleyPark.Common
{
    public class WaymarkEvent : PubSubEvent<RESTWaymark> { }

    public class LoadPresetEvent : PubSubEvent<string> { }

    public class SavePresetEvent : PubSubEvent<string> { }
}
