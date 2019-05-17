﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Input.Bindings;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays.Changelog;
using osuTK.Graphics;

namespace osu.Game.Overlays
{
    public class ChangelogOverlay : FullscreenOverlay
    {
        private ChangelogHeader header;

        private BadgeDisplay badges;

        private ChangelogContent listing;
        private ChangelogContent content;

        private ScrollContainer scroll;

        private SampleChannel sampleBack;

        private float savedScrollPosition;

        [BackgroundDependencyLoader]
        private void load(AudioManager audio, OsuColour colour)
        {
            // these possibly need adjusting?
            Waves.FirstWaveColour = colour.Violet;
            Waves.SecondWaveColour = OsuColour.FromHex(@"8F03BF");
            Waves.ThirdWaveColour = OsuColour.FromHex(@"600280");
            Waves.FourthWaveColour = OsuColour.FromHex(@"300140");

            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(49, 36, 54, 255),
                },
                scroll = new ScrollContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    ScrollbarVisible = false,
                    Child = new ReverseChildIDFillFlowContainer<Drawable>
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Children = new Drawable[]
                        {
                            header = new ChangelogHeader(),
                            badges = new BadgeDisplay(),
                            listing = new ChangelogContent(),
                            content = new ChangelogContent()
                        },
                    },
                },
            };

            header.ListingSelected += ShowListing;

            // todo: better
            badges.Current.ValueChanged += e =>
            {
                if (e.NewValue?.LatestBuild != null)
                    ShowBuild(e.NewValue.LatestBuild);
            };

            listing.BuildSelected += ShowBuild;
            content.BuildSelected += ShowBuild;

            sampleBack = audio.Sample.Get(@"UI/generic-select-soft"); // @"UI/screen-back" feels non-fitting here
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            fetchListing();
        }

        protected override void PopIn()
        {
            base.PopIn();
            FadeEdgeEffectTo(0.25f, WaveContainer.APPEAR_DURATION, Easing.In);
        }

        protected override void PopOut()
        {
            base.PopOut();
            FadeEdgeEffectTo(0, WaveContainer.DISAPPEAR_DURATION, Easing.Out);
        }

        public override bool OnPressed(GlobalAction action)
        {
            switch (action)
            {
                case GlobalAction.Back:
                    if (isAtListing)
                    {
                        if (scroll.Current > scroll.GetChildPosInContent(listing))
                        {
                            scroll.ScrollTo(0);
                            sampleBack?.Play();
                        }
                        else
                            State = Visibility.Hidden;
                    }
                    else
                    {
                        ShowListing();
                        sampleBack?.Play();
                    }

                    return true;
            }

            return false;
        }

        private void fetchListing()
        {
            header.ShowListing();

            var req = new GetChangelogRequest();
            req.Success += res =>
            {
                // remap streams to builds to ensure model equality
                res.Builds.ForEach(b => b.UpdateStream = res.Streams.Find(s => s.Id == b.UpdateStream.Id));
                res.Streams.ForEach(s => s.LatestBuild.UpdateStream = res.Streams.Find(s2 => s2.Id == s.LatestBuild.UpdateStream.Id));

                listing.ShowListing(res.Builds);
                badges.Populate(res.Streams);
            };

            API.Queue(req);
        }

        private bool isAtListing;

        public void ShowListing()
        {
            isAtListing = true;
            header.ShowListing();

            content.Hide();
            badges.Current.Value = null;
            listing.Show();
            scroll.ScrollTo(savedScrollPosition);
        }

        /// <summary>
        /// Fetches and shows a specific build from a specific update stream.
        /// </summary>
        /// <param name="build">Must contain at least <see cref="APIUpdateStream.Name"/> and
        /// <see cref="APIChangelogBuild.Version"/>. If <see cref="APIUpdateStream.DisplayName"/> and
        /// <see cref="APIChangelogBuild.DisplayVersion"/> are specified, the header will instantly display them.</param>
        public void ShowBuild(APIChangelogBuild build)
        {
            if (build == null)
            {
                ShowListing();
                return;
            }

            header.ShowBuild(build.UpdateStream.DisplayName, build.DisplayVersion);
            badges.Current.Value = build.UpdateStream;

            listing.Hide();

            void displayBuild(APIChangelogBuild populatedBuild)
            {
                content.Show();
                content.ShowBuild(populatedBuild);

                if (scroll.Current > scroll.GetChildPosInContent(content))
                    scroll.ScrollTo(content);

                if (isAtListing)
                    savedScrollPosition = scroll.Current;
                isAtListing = false;
            }

            if (build.Versions != null)
                displayBuild(build);
            else
            {
                var req = new GetChangelogBuildRequest(build.UpdateStream.Name, build.Version);
                req.Success += displayBuild;
                API.Queue(req);
            }
        }
    }
}
