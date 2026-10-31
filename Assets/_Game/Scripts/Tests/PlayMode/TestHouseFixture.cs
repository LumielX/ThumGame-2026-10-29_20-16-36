using System.Collections;
using SpiritMovers.Job;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace SpiritMovers.Tests
{
    /// <summary>The M3C greybox house: <see cref="TestRoomFixture"/> plus the house, the job director and Thai UI.</summary>
    public abstract class TestHouseFixture : TestRoomFixture
    {
        protected override string ScenePath => "Assets/_Game/Scenes/M3_TestHouse.unity";

        protected HouseAuthoring House { get; private set; }
        protected JobDirector Director { get; private set; }

        protected override IEnumerator BeforeHost()
        {
            yield return LocalizationSettings.InitializationOperation;
            LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale("th");
            House = Object.FindAnyObjectByType<HouseAuthoring>();
            Director = Object.FindAnyObjectByType<JobDirector>();
        }

        /// <summary>Server: put a slot's item exactly at its remembered place.</summary>
        protected IEnumerator PutBack(string slotId)
        {
            Director.PlaceAtTarget(slotId);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
        }
    }
}
