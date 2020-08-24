// BarMultiButton.cs: Bar item containing multiple buttons.
//
// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt


namespace Morphic.Bar.Bar
{
    using System.Collections.Generic;
    using Actions;
    using Newtonsoft.Json;
    using UI.BarControls;

    /// <summary>
    /// Bar item that contains multiple buttons.
    /// </summary>
    [JsonTypeName("multi")]
    [BarControl(typeof(MultiButtonBarControl))]
    public class BarMultiButton : BarItem
    {
        [JsonProperty("configuration.buttons")]
        public Dictionary<string, ButtonInfo> Buttons { get; set; } = new Dictionary<string, ButtonInfo>();

        [JsonObject(MemberSerialization.OptIn)]
        public class ButtonInfo
        {
            private string? value = null;

            /// <summary>
            /// Display text.
            /// </summary>
            [JsonProperty("label")]
            public string Text { get; set; } = string.Empty;

            /// <summary>
            /// Unique identifier. Of omitted, the key from BarMultiButton.Buttons is used.
            /// </summary>
            [JsonProperty("id")]
            public string Id { get; set; } = null!;

            /// <summary>
            /// The value to pass to the action when this button is clicked.
            /// Used by `kind = "internal"`, specifying "{button}" as an argument value will resolve to this value
            /// (or the id, if not set)
            /// </summary>
            [JsonProperty("value")]
            public string Value
            {
                get => this.value ?? this.Id;
                set => this.value = value;
            }

            [JsonProperty("action")]
            public BarAction Action { get; set; } = new NoOpAction();
        }

        public override void Deserialized(BarData bar)
        {
            base.Deserialized(bar);

            foreach (var (key, buttonInfo) in this.Buttons)
            {
                if (string.IsNullOrEmpty(buttonInfo.Id))
                {
                    buttonInfo.Id = key;
                }
            }
        }
    }
}
