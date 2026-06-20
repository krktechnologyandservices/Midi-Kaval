using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Models.Notifications;

namespace MidiKaval.Api.Infrastructure.Notifications;

public static class NotificationPreferencesDefaults
{
    public static NotificationPreferencesDto ForRole(string role) =>
        role switch
        {
            UserRoles.CaseWorker or UserRoles.SocialWorker => new NotificationPreferencesDto
            {
                PushEnabled = true,
                EmailEnabled = false,
                Channels = new NotificationPreferenceChannelsDto
                {
                    Visits = true,
                    Court = true,
                    Interventions = true,
                    Claims = true,
                },
            },
            UserRoles.Coordinator or UserRoles.Director => new NotificationPreferencesDto
            {
                PushEnabled = false,
                EmailEnabled = true,
                Channels = new NotificationPreferenceChannelsDto
                {
                    Visits = false,
                    Court = true,
                    Interventions = true,
                    Claims = true,
                    Reports = true,
                    Assignments = true,
                },
            },
            _ => new NotificationPreferencesDto
            {
                PushEnabled = false,
                EmailEnabled = false,
                Channels = new NotificationPreferenceChannelsDto(),
            },
        };
}
