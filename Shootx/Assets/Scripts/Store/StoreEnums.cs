using System;

namespace Shootx.Store
{
    public enum ItemRarity
    {
        Common = 0,
        Uncommon = 1,
        Rare = 2,
        Epic = 3,
        Legendary = 4
    }

    public enum CurrencyType
    {
        SoftCurrency = 0,
        HardCurrency = 1
    }

    public enum StoreItemType
    {
        Weapon = 0,
        Outfit = 1
    }

    public enum MultiplayerAccessState
    {
        NoAccess = 0,
        TemporaryAccess = 1,
        PermanentAccess = 2
    }
}
