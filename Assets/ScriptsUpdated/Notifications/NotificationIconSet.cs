using System;
using UnityEngine;

/// <summary>
/// Maps each NotificationType to a sprite shown in the notification row.
/// Create via Assets → Create → Game → Notification Icon Set, then assign sprites per type.
/// </summary>
[CreateAssetMenu(fileName = "NotificationIconSet", menuName = "Game/Notification Icon Set")]
public class NotificationIconSet : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public NotificationType type;
        public Sprite icon;
    }

    [SerializeField] private Entry[] entries;
    [SerializeField] public Sprite deathIcon;

    public Sprite GetIcon(NotificationType type)
    {
        if (entries == null) return null;
        for (int i = 0; i < entries.Length; i++)
            if (entries[i].type == type) return entries[i].icon;
        return null;
    }

    private void Reset()
    {
        entries = new Entry[]
        {
            new Entry { type = NotificationType.GatheringCompleted },
            new Entry { type = NotificationType.GatheringFailed    },
            new Entry { type = NotificationType.DiscoveryCompleted },
            new Entry { type = NotificationType.DiscoveryFailed    },
            new Entry { type = NotificationType.BuildingCompleted  },
            new Entry { type = NotificationType.BuildingOnFire     },
            new Entry { type = NotificationType.BuildingDamaged    },
            new Entry { type = NotificationType.BuildingDestroyed  },
            new Entry { type = NotificationType.ResearchCompleted      },
            new Entry { type = NotificationType.ResearchFailed         },
            new Entry { type = NotificationType.BirthSucceeded                 },
            new Entry { type = NotificationType.BirthFailed                  },
            new Entry { type = NotificationType.ProductionCompleted              },
            new Entry { type = NotificationType.ProductionPausedLackOfResources },
            new Entry { type = NotificationType.ProductionPausedLackOfWorkers   },
            new Entry { type = NotificationType.CraftingCompleted               },
            new Entry { type = NotificationType.CraftingFailedWeather           },
            new Entry { type = NotificationType.FireFightSucceeded              },
            new Entry { type = NotificationType.FireFightFailed                 },
            new Entry { type = NotificationType.BuildingFlooded                 },
        };
    }
}
