using System.Collections.Generic;

namespace WSOYappinator
{
    public interface IEventPriorityLoader
    {
        Dictionary<string, int> Load(string audioSetFolder);
    }
}
