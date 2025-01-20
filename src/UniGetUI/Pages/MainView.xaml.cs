using Windows.System;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using UniGetUI.Core.Data;
using UniGetUI.Core.SettingsEngine;
using UniGetUI.Core.Tools;
using UniGetUI.Interface.Dialogs;
using UniGetUI.Interface.Pages;
using UniGetUI.Interface.Pages.LogPage;
using UniGetUI.Interface.SoftwarePages;
using Windows.UI.Core;
using UniGetUI.PackageEngine.Interfaces;
using UniGetUI.Pages.DialogPages;
using UniGetUI.PackageEngine.Operations;
using CommunityToolkit.WinUI.Controls;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using UniGetUI.Core.Logging;
using UniGetUI.Interface.Enums;
using UniGetUI.PackageEngine.Enums;
using UniGetUI.PackageEngine;
using UniGetUI.PackageOperations;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace UniGetUI.Interface
{
    public enum PageType
    {
        Discover,
        Updates,
        Installed,
        Bundles,
        Settings,
        OwnLog,
        ManagerLog,
        OperationHistory,
        Help,
        Null // Used for initializers
    }

    public sealed partial class MainView
    {
        private PageType OldPage_t = PageType.Null;
        private PageType CurrentPage_t = PageType.Null;

        public MainView()
        {
            InitializeComponent();
            OperationList.ItemContainerTransitions = null;
            OperationList.ItemsSource = MainApp.Operations._operationList;

            PEInterface.UpgradablePackagesLoader.PackagesChanged += (s, e) =>
            {
                MainApp.Dispatcher.TryEnqueue(() =>
                {
                    MainApp.Tooltip.AvailableUpdates = PEInterface.UpgradablePackagesLoader.Count();
                    UpdatesBadge.Value = PEInterface.UpgradablePackagesLoader.Count();
                    UpdatesBadge.Visibility = UpdatesBadge.Value > 0 ? Visibility.Visible : Visibility.Collapsed;
                });
            };

            PEInterface.UpgradablePackagesLoader.FinishedLoading += (s, e) =>
            {
                MainApp.Dispatcher.TryEnqueue(() =>
                {

                    List<IPackage> upgradablePackages = [];
                    foreach (IPackage package in PEInterface.UpgradablePackagesLoader.Packages)
                        if (package.Tag is not PackageTag.OnQueue and not PackageTag.BeingProcessed)
                            upgradablePackages.Add(package);

                    try
                    {
                        if (upgradablePackages.Count == 0)
                            return;

                        bool EnableAutoUpdate = Settings.Get("AutomaticallyUpdatePackages") ||
                                                Environment.GetCommandLineArgs().Contains("--updateapps");

                        if (EnableAutoUpdate)
                        {
                            foreach (IPackage package in PEInterface.UpgradablePackagesLoader.Packages)
                                if (package.Tag is not PackageTag.BeingProcessed and not PackageTag.OnQueue)
                                    MainApp.Operations.Update(package);
                        }

                        if (Settings.AreUpdatesNotificationsDisabled())
                            return;

                        AppNotificationManager.Default.RemoveByTagAsync(CoreData.UpdatesAvailableNotificationTag
                            .ToString());


                        AppNotification notification;
                        if (upgradablePackages.Count == 1)
                        {
                            if (EnableAutoUpdate)
                            {
                                AppNotificationBuilder builder = new AppNotificationBuilder()
                                    .SetScenario(AppNotificationScenario.Default)
                                    .SetTag(CoreData.UpdatesAvailableNotificationTag.ToString())

                                    .AddText(CoreTools.Translate("An update was found!"))
                                    .AddText(CoreTools.Translate("{0} is being updated to version {1}",
                                        upgradablePackages[0].Name, upgradablePackages[0].NewVersion))
                                    .SetAttributionText(CoreTools.Translate("You have currently version {0} installed",
                                        upgradablePackages[0].Version))

                                    .AddArgument("action", NotificationArguments.ShowOnUpdatesTab);
                                notification = builder.BuildNotification();
                            }
                            else
                            {
                                AppNotificationBuilder builder = new AppNotificationBuilder()
                                    .SetScenario(AppNotificationScenario.Default)
                                    .SetTag(CoreData.UpdatesAvailableNotificationTag.ToString())

                                    .AddText(CoreTools.Translate("An update was found!"))
                                    .AddText(CoreTools.Translate("{0} can be updated to version {1}",
                                        upgradablePackages[0].Name, upgradablePackages[0].NewVersion))
                                    .SetAttributionText(CoreTools.Translate("You have currently version {0} installed",
                                        upgradablePackages[0].Version))

                                    .AddArgument("action", NotificationArguments.ShowOnUpdatesTab)
                                    .AddButton(new AppNotificationButton(CoreTools.Translate("View on UniGetUI")
                                            .Replace("'", "´"))
                                        .AddArgument("action", NotificationArguments.ShowOnUpdatesTab)
                                    )
                                    .AddButton(new AppNotificationButton(CoreTools.Translate("Update"))
                                        .AddArgument("action", NotificationArguments.UpdateAllPackages)
                                    );
                                notification = builder.BuildNotification();
                            }
                        }
                        else
                        {
                            string attribution = "";
                            foreach (IPackage package in upgradablePackages) attribution += package.Name + ", ";
                            attribution = attribution.TrimEnd(' ').TrimEnd(',');

                            if (EnableAutoUpdate)
                            {

                                AppNotificationBuilder builder = new AppNotificationBuilder()
                                    .SetScenario(AppNotificationScenario.Default)
                                    .SetTag(CoreData.UpdatesAvailableNotificationTag.ToString())

                                    .AddText(
                                        CoreTools.Translate("{0} packages are being updated", upgradablePackages.Count))
                                    .SetAttributionText(attribution)
                                    .AddText(CoreTools.Translate("Updates found!"))

                                    .AddArgument("action", NotificationArguments.ShowOnUpdatesTab);
                                notification = builder.BuildNotification();
                            }
                            else
                            {
                                AppNotificationBuilder builder = new AppNotificationBuilder()
                                    .SetScenario(AppNotificationScenario.Default)
                                    .SetTag(CoreData.UpdatesAvailableNotificationTag.ToString())

                                    .AddText(CoreTools.Translate("Updates found!"))
                                    .AddText(CoreTools.Translate("{0} packages can be updated",
                                        upgradablePackages.Count))
                                    .SetAttributionText(attribution)

                                    .AddButton(new AppNotificationButton(CoreTools.Translate("Open UniGetUI")
                                            .Replace("'", "´"))
                                        .AddArgument("action", NotificationArguments.ShowOnUpdatesTab)
                                    )
                                    .AddButton(new AppNotificationButton(CoreTools.Translate("Update all"))
                                        .AddArgument("action", NotificationArguments.UpdateAllPackages)
                                    )
                                    .AddArgument("action", NotificationArguments.ShowOnUpdatesTab);
                                notification = builder.BuildNotification();
                            }
                        }

                        notification.ExpiresOnReboot = true;
                        AppNotificationManager.Default.Show(notification);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex);
                    }
                });
            };

            MoreNavButtonMenu.Closed += (_, _) => SelectNavButtonForPage(CurrentPage_t);
            KeyUp += (s, e) =>
            {
                bool IS_CONTROL_PRESSED = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
                bool IS_SHIFT_PRESSED = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);

                if (e.Key is VirtualKey.Tab && IS_CONTROL_PRESSED)
                {
                    NavigateTo(IS_SHIFT_PRESSED ? GetPreviousPage(CurrentPage_t) : GetNextPage(CurrentPage_t));
                }
                else if (e.Key == VirtualKey.F1)
                {
                    NavigateTo(PageType.Help);
                }
                else if ((e.Key is VirtualKey.Q or VirtualKey.W) && IS_CONTROL_PRESSED)
                {
                    MainApp.Instance.MainWindow.Close();
                }
                else if (e.Key is VirtualKey.F5 || (e.Key is VirtualKey.R && IS_CONTROL_PRESSED))
                {
                    (NavFrame.Content as IKeyboardShortcutListener)?.ReloadTriggered();
                }
                else if (e.Key is VirtualKey.F && IS_CONTROL_PRESSED)
                {
                    (NavFrame.Content as IKeyboardShortcutListener)?.SearchTriggered();
                }
                else if (e.Key is VirtualKey.A && IS_CONTROL_PRESSED)
                {
                    (NavFrame.Content as IKeyboardShortcutListener)?.SelectAllTriggered();
                }
            };

            LoadDefaultPage();

            if (CoreTools.IsAdministrator() && !Settings.Get("AlreadyWarnedAboutAdmin"))
            {
                Settings.Set("AlreadyWarnedAboutAdmin", true);
                DialogHelper.WarnAboutAdminRights();
            }

            UpdateOperationsLayout();
            MainApp.Operations._operationList.CollectionChanged += (_, _) => UpdateOperationsLayout();
        }

        public void LoadDefaultPage()
        {
            PageType type = Settings.GetValue("StartupPage") switch
            {
                "discover" => PageType.Discover,
                "updates" => PageType.Updates,
                "installed" => PageType.Installed,
                "bundles" => PageType.Bundles,
                "settings" => PageType.Settings,
                _ => MainApp.Tooltip.AvailableUpdates > 0 ? PageType.Updates : PageType.Discover
            };
            NavigateTo(type);
        }

        private void DiscoverNavButton_Click(object sender, EventArgs e)
            => NavigateTo(PageType.Discover);

        private void InstalledNavButton_Click(object sender, EventArgs e)
            => NavigateTo(PageType.Installed);

        private void UpdatesNavButton_Click(object sender, EventArgs e)
            => NavigateTo(PageType.Updates);

        private void BundlesNavButton_Click(object sender, EventArgs e)
            => NavigateTo(PageType.Bundles);

        private void MoreNavButton_Click(object sender, EventArgs e)
        {
            SelectNavButtonForPage(PageType.OwnLog);
            (VersionMenuItem as MenuFlyoutItem).Text = CoreTools.Translate("WingetUI Version {0}", CoreData.VersionName);
            MoreNavButtonMenu.ShowAt(MoreNavButton, new FlyoutShowOptions { ShowMode = FlyoutShowMode.Standard });
        }

        private static Type GetClassTypeForType(PageType type)
            => type switch
            {
                PageType.Discover => typeof(DiscoverSoftwarePage),
                PageType.Updates => typeof(SoftwareUpdatesPage),
                PageType.Installed => typeof(InstalledPackagesPage),
                PageType.Bundles => typeof(PackageBundlesPage),
                PageType.Settings => typeof(SettingsPage),
                PageType.OwnLog => typeof(UniGetUILogPage),
                PageType.ManagerLog => typeof(ManagerLogsPage),
                PageType.OperationHistory => typeof(OperationHistoryPage),
                PageType.Help => typeof(HelpPage),
                PageType.Null => throw new InvalidCastException("Page type is Null"),
                _ => throw new InvalidDataException($"Unknown page type {type}")
            };


        private static PageType GetNextPage(PageType type)
            => type switch
            {
                // Default loop
                PageType.Discover => PageType.Updates,
                PageType.Updates => PageType.Installed,
                PageType.Installed => PageType.Bundles,
                PageType.Bundles => PageType.Settings,
                PageType.Settings => PageType.Discover,

                // "Extra" pages
                PageType.OperationHistory => PageType.Discover,
                PageType.OwnLog => PageType.Discover,
                PageType.ManagerLog => PageType.Discover,
                PageType.Help => PageType.Discover,
                PageType.Null => PageType.Discover,
                _ => throw new InvalidDataException($"Unknown page type {type}")
            };

        private static PageType GetPreviousPage(PageType type)
            => type switch
            {
                // Default loop
                PageType.Discover => PageType.Settings,
                PageType.Updates => PageType.Discover,
                PageType.Installed => PageType.Updates,
                PageType.Bundles => PageType.Installed,
                PageType.Settings => PageType.Bundles,

                // "Extra" pages
                PageType.OperationHistory => PageType.Discover,
                PageType.OwnLog => PageType.Discover,
                PageType.ManagerLog => PageType.Discover,
                PageType.Help => PageType.Discover,
                PageType.Null => PageType.Discover,
                _ => throw new InvalidDataException($"Unknown page type {type}")
            };

        private void SettingsNavButton_Click(object sender, EventArgs e)
            => NavigateTo(PageType.Settings);

        private void SelectNavButtonForPage(PageType page)
        {
            DiscoverNavButton.IsChecked = page is PageType.Discover;
            UpdatesNavButton.IsChecked = page is PageType.Updates;
            InstalledNavButton.IsChecked = page is PageType.Installed;
            BundlesNavButton.IsChecked = page is PageType.Bundles;

            SettingsNavButton.IsChecked = page is PageType.Settings;
            AboutNavButton.IsChecked = false;
            MoreNavButton.IsChecked = page is PageType.Help or PageType.ManagerLog or PageType.OperationHistory or PageType.OwnLog;
        }

        private async void AboutNavButton_Click(object sender, EventArgs e)
        {
            SelectNavButtonForPage(PageType.Null);
            AboutNavButton.IsChecked = true;
            await DialogHelper.ShowAboutUniGetUI();
            SelectNavButtonForPage(CurrentPage_t);
        }

        public void NavigateTo(PageType NewPage_t, object? arguments = null)
        {
            SelectNavButtonForPage(NewPage_t);
            // if (CurrentPage_t == NewPage_t) return;

            //Page NewPage = GetPageForType(NewPage_t);
            OldPage_t = NewPage_t;
            Type NewType = GetClassTypeForType(NewPage_t);

            //MainContentPresenterGrid.Children.Clear();
            //if (!MainContentPresenterGrid.Children.Contains(NewPage))
            //{
                // if(NewPage_t != PageType.Installed) AddedPages.Add(NewPage);
                //Grid.SetColumn(NewPage, 0);
                //Grid.SetRow(NewPage, 0);
                //MainContentPresenterGrid.Children.Add(NewPage);
                try
                {
                    NavFrame.Navigate(NewType, arguments);
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to navigate to page {NewPage_t}");
                    Logger.Error(ex);
                }
            //}

            /*foreach (Page page in AddedPages)
            {
                bool IS_MAIN_PAGE = (page == NewPage);
                page.Visibility =  IS_MAIN_PAGE? Visibility.Visible : Visibility.Collapsed;
                page.IsEnabled = IS_MAIN_PAGE;
            }

            OldPage_t = CurrentPage_t;
            CurrentPage_t = NewPage_t;

            (NewPage as AbstractPackagesPage)?.FocusPackageList();
            (NewPage as IEnterLeaveListener)?.OnEnter();
            if (OldPage_t is not PageType.Null)
            {
                (CurrentPage as IEnterLeaveListener)?.OnLeave();
                if(OldPage_t is PageType.Installed) (CurrentPage as IDisposable)?.Dispose();
            }

            CurrentPage = NewPage;*/
        }

        private void ReleaseNotesMenu_Click(object sender, RoutedEventArgs e)
            => DialogHelper.ShowReleaseNotes();

        private void OperationHistoryMenu_Click(object sender, RoutedEventArgs e)
            => NavigateTo(PageType.OperationHistory);

        private void ManagerLogsMenu_Click(object sender, RoutedEventArgs e)
            => NavigateTo(PageType.ManagerLog);

        public void UniGetUILogs_Click(object sender, RoutedEventArgs e)
            => NavigateTo(PageType.OwnLog);

        private void HelpMenu_Click(object sender, RoutedEventArgs e)
            => NavigateTo(PageType.Help);

        private void QuitUniGetUI_Click(object sender, RoutedEventArgs e)
            => MainApp.Instance.DisposeAndQuit();

        bool isCollapsed;

        private void UpdateOperationsLayout()
        {
            int OpCount = MainApp.Operations._operationList.Count;
            int maxHeight = Math.Max((OpCount * 58) - 7, 0);

            MainContentPresenterGrid.RowDefinitions[2].MaxHeight = maxHeight;

            if (OpCount > 0)
            {
                if(isCollapsed)
                {
                    MainContentPresenterGrid.RowDefinitions[2].Height = new GridLength(0);
                    MainContentPresenterGrid.RowDefinitions[1].Height = new GridLength(16);
                    OperationSplitter.Visibility = Visibility.Visible;
                    OperationSplitterMenuButton.Visibility = Visibility.Visible;
                    OperationSplitter.IsEnabled = false;
                }
                else
                {
                    MainContentPresenterGrid.RowDefinitions[2].Height = new GridLength(Math.Min(maxHeight, 200));
                    MainContentPresenterGrid.RowDefinitions[1].Height = new GridLength(16);
                    OperationSplitter.Visibility = Visibility.Visible;
                    OperationSplitterMenuButton.Visibility = Visibility.Visible;
                    OperationSplitter.IsEnabled = true;
                }
            }
            else
            {
                MainContentPresenterGrid.RowDefinitions[1].Height = new GridLength(0);
                MainContentPresenterGrid.RowDefinitions[2].Height = new GridLength(0);
                OperationSplitter.Visibility = Visibility.Collapsed;
                OperationSplitterMenuButton.Visibility = Visibility.Collapsed;
            }
        }

        private void OperationSplitterMenuButton_Click(object sender, RoutedEventArgs e)
        {
            OperationListMenu.ShowAt(OperationSplitterMenuButton, new FlyoutShowOptions { ShowMode = FlyoutShowMode.Standard });
        }

        private void ExpandCollapseOpList_Click(object sender, RoutedEventArgs e)
        {
            if (isCollapsed)
            {
                isCollapsed = false;
                ExpandCollapseOpList.Content = new FontIcon() { Glyph = "\uE96E", FontSize = 14 };
                UpdateOperationsLayout();
            }
            else
            {
                isCollapsed = true;
                ExpandCollapseOpList.Content = new FontIcon() { Glyph = "\uE96D", FontSize = 14 };
                UpdateOperationsLayout();
            }
        }

        private void CancellAllOps_Click(object sender, RoutedEventArgs e)
        {
            foreach (var widget in MainApp.Operations._operationList)
            {
                var operation = widget.Operation;
                if (operation.Status is OperationStatus.InQueue or OperationStatus.Running)
                    operation.Cancel();
            }
        }

        private void RetryFailedOps_Click(object sender, RoutedEventArgs e)
        {
            foreach (var widget in MainApp.Operations._operationList)
            {
                var operation = widget.Operation;
                if (operation.Status is OperationStatus.Failed)
                    operation.Retry(AbstractOperation.RetryMode.Retry);
            }
        }

        private void ClearSuccessfulOps_Click(object sender, RoutedEventArgs e)
        {
            foreach (var widget in MainApp.Operations._operationList.ToArray())
            {
                var operation = widget.Operation;
                if (operation.Status is OperationStatus.Succeeded)
                    widget.Close();
            }
        }
    }
}
