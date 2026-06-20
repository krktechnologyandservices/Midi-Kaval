using MidiKaval.Api.Controllers;

namespace MidiKaval.Api.UnitTests;

public class HealthControllerTests
{
    [Fact]
    public void HealthController_Type_Exists()
    {
        Assert.NotNull(typeof(HealthController));
    }
}
