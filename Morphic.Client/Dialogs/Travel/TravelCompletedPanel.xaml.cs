﻿// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt
//
// The R&D leading to these results received funding from the:
// * Rehabilitation Services Administration, US Dept. of Education under 
//   grant H421A150006 (APCP)
// * National Institute on Disability, Independent Living, and 
//   Rehabilitation Research (NIDILRR)
// * Administration for Independent Living & Dept. of Education under grants 
//   H133E080022 (RERC-IT) and H133E130028/90RE5003-01-00 (UIITA-RERC)
// * European Union's Seventh Framework Programme (FP7/2007-2013) grant 
//   agreement nos. 289016 (Cloud4all) and 610510 (Prosperity4All)
// * William and Flora Hewlett Foundation
// * Ontario Ministry of Research and Innovation
// * Canadian Foundation for Innovation
// * Adobe Foundation
// * Consumer Electronics Association Foundation

namespace Morphic.Client.Dialogs
{
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using Microsoft.Extensions.Logging;
    using Service;

    /// <summary>
    /// Shown at the end of the capture process as a review for the user
    /// </summary>
    public partial class TravelCompletedPanel : StackPanel
    {

        #region Creating a Panel

        public TravelCompletedPanel(MorphicSession morphicSession, ILogger<TravelCompletedPanel> logger)
        {
            this.morphicSession = morphicSession;
            this.logger = logger;
            this.InitializeComponent();
        }

        /// <summary>
        /// A logger to use
        /// </summary>
        private readonly ILogger<TravelCompletedPanel> logger;

        #endregion

        #region Completion Events

        /// <summary>
        /// The event that is dispatched when the user clicks the Close button
        /// </summary>
        public event EventHandler? Completed;

        #endregion

        #region Lifecycle

        private readonly MorphicSession morphicSession;

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            this.Loaded += this.OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            this.EmailLabel.Content = this.morphicSession.User?.Email;
        }

        #endregion

        #region Actions

        /// <summary>
        /// Handler for when the user clicks the Close button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnClose(object? sender, RoutedEventArgs e)
        {
            this.Completed?.Invoke(this, new EventArgs());
        }

        #endregion
    }
}
