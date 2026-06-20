using Cytrus.Models;
using Cytrus.Selection;

namespace Cytrus.Planning;

public interface IDownloadPlanner
{
    FragmentPlan Plan(FragmentInfo fragment, IFileSelector selector, PlannerOptions options);
}
