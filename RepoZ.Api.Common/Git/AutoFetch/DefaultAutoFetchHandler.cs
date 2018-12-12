﻿using System;
using System.Collections.Generic;
using System.Threading;
using RepoZ.Api.Common.Common;
using RepoZ.Api.Git;

namespace RepoZ.Api.Common.Git.AutoFetch
{
    public class DefaultAutoFetchHandler : IAutoFetchHandler
    {
        private bool _active;
        private AutoFetchMode? _mode = null;
		private Timer _timer;
		private Dictionary<AutoFetchMode, AutoFetchProfile> _profiles;
		private int _lastFetchRepository = -1;

        public DefaultAutoFetchHandler(IAppSettingsService appSettingsService,
			IRepositoryInformationAggregator repositoryInformationAggregator,
			IRepositoryWriter repositoryWriter)
        {
            AppSettingsService = appSettingsService ?? throw new ArgumentNullException(nameof(appSettingsService));
			RepositoryInformationAggregator = repositoryInformationAggregator ?? throw new ArgumentNullException(nameof(repositoryInformationAggregator));
			RepositoryWriter = repositoryWriter ?? throw new ArgumentNullException(nameof(repositoryWriter));
			AppSettingsService.RegisterInvalidationHandler(() => Mode = AppSettingsService.AutoFetchMode);

			_profiles = new Dictionary<AutoFetchMode, AutoFetchProfile>
			{
				{ AutoFetchMode.Off, new AutoFetchProfile() { PauseBetweenFetches = TimeSpan.MaxValue } },
				{ AutoFetchMode.Discretely, new AutoFetchProfile() { PauseBetweenFetches = TimeSpan.FromMinutes(5) } },
				{ AutoFetchMode.Adequate, new AutoFetchProfile() { PauseBetweenFetches = TimeSpan.FromMinutes(1) } },
				{ AutoFetchMode.Aggresive, new AutoFetchProfile() { PauseBetweenFetches = TimeSpan.FromSeconds(2) } }
			};

			_timer = new Timer(FetchNext, null, Timeout.Infinite, Timeout.Infinite);
		}

		private void UpdateBehavior()
		{
			if (!_mode.HasValue)
				return;

			UpdateBehavior(_mode.Value);
		}

		private void UpdateBehavior(AutoFetchMode mode)
		{
			var profile = _profiles[mode];

			var milliseconds = (int)profile.PauseBetweenFetches.TotalMilliseconds;
			if (profile.PauseBetweenFetches == TimeSpan.MaxValue)
				milliseconds = Timeout.Infinite;

			_timer.Change(milliseconds, Timeout.Infinite);
		}

		private void FetchNext(object timerState)
		{
			var count = RepositoryInformationAggregator.Repositories?.Count ?? 0;

			if (count == 0)
				return;

			// temporarily disable the timer to prevent parallel fetch executions
			UpdateBehavior(AutoFetchMode.Off);

			try
			{
				_lastFetchRepository++;

				if (count <= _lastFetchRepository)
					_lastFetchRepository = 0;

				var repositoryView = RepositoryInformationAggregator.Repositories[_lastFetchRepository];
				Console.WriteLine("Auto-fetching " + repositoryView.Name);

				// TODO: process might never return and therefore the timer is not enabled again ...
				RepositoryWriter.Fetch(repositoryView.Repository);
			}
			catch
			{
				// nothing to see here
			}
			finally
			{
				// re-enable the timer to get to the next fetch
				UpdateBehavior();
			}
		}

        public bool Active
        {
            get => _active;
            set
            {
                _active = value;

				if (value && _mode == null)
                    Mode = AppSettingsService.AutoFetchMode;

				UpdateBehavior();
			}
        }

        public AutoFetchMode Mode
        {
            get => _mode ?? AutoFetchMode.Off;
            set
            {
                if (value == _mode)
                    return;

				_mode = value;
				Console.WriteLine("Auto fetch is: " + _mode.GetValueOrDefault().ToString());

				UpdateBehavior(); 
            }
        }

		public IAppSettingsService AppSettingsService { get; }

		public IRepositoryInformationAggregator RepositoryInformationAggregator { get; }

		public IRepositoryWriter RepositoryWriter { get; }
	}
}
