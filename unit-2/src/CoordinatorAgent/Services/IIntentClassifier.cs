using CoordinatorAgent.Models;

namespace CoordinatorAgent.Services;

public interface IIntentClassifier
{
    Intent? Classify(string query);
}
