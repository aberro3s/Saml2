﻿using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using FluentAssertions;
using System.IdentityModel.Metadata;
using System.Linq;
using Kentor.AuthServices.Configuration;
using Kentor.AuthServices.Metadata;
using Kentor.AuthServices.TestHelpers;
using Kentor.AuthServices.Tests.Metadata;

namespace Kentor.AuthServices.Tests
{
    [TestClass]
    public class FederationTests
    {
        TimeSpan refreshMinInterval = MetadataRefreshScheduler.minInternval;

        [TestCleanup]
        public void Cleanup()
        {
            MetadataServer.IdpVeryShortCacheDurationIncludeInvalidKey = false;
            MetadataServer.FederationVeryShortCacheDurationSecondAlternativeEnabled = false;
            MetadataRefreshScheduler.minInternval = refreshMinInterval;
        }

        [TestMethod]
        public void Federation_Ctor_NullcheckConfig()
        {
            Action a = () => new Federation(null, Options.FromConfiguration);

            a.ShouldThrow<ArgumentNullException>().And.ParamName.Should().Be("config");
        }

        [TestMethod]
        public void Federation_LoadSambiTestMetadata()
        {
            // Sambi is the Swedish health care federation. To test that AuthServices
            // handles some real world metadata, the metadadata from Sambi's test
            // environment is used.

            var url = new Uri("http://localhost:13428/SambiMetadata");

            Action a = () => new Federation(url, true, StubFactory.CreateOptions());

            a.ShouldNotThrow();
        }

        [TestMethod]
        public void Federation_LoadSkolfederationMetadata()
        {
            // Skolfederation is the Swedish national school federation. To test that
            // AuthServices handles some real world metadata, the metdata from the
            // skolfederation federation is used.

            var url = new Uri("http://localhost:13428/SkolfederationMetadata");

            Action a = () => new Federation(url, true, StubFactory.CreateOptions());

            a.ShouldNotThrow();
        }

        [TestMethod]
        public void Federation_Ctor_MetadataUrl()
        {
            var options = StubFactory.CreateOptions();

            var subject = new Federation(
                new Uri("http://localhost:13428/federationMetadata"),
                false,
                options);

            IdentityProvider idp;
            options.IdentityProviders
                .TryGetValue(new EntityId("http://idp.federation.example.com/metadata"), out idp)
                .Should().BeTrue();
        }

        [TestMethod]
        public void Federation_MetadataValidUntil_Loaded()
        {
            var subject = new Federation(
                new Uri("http://localhost:13428/federationMetadata"),
                false,
                StubFactory.CreateOptions());

            subject.MetadataValidUntil.Should().Be(new DateTime(2100, 01, 01, 14, 43, 15));
        }

        [TestMethod]
        public void Federation_MetadataValidUntil_CalculatedFromCacheDuration()
        {
            var subject = new Federation(
                new Uri("http://localhost:13428/federationMetadataVeryShortCacheDuration"),
                false,
                StubFactory.CreateOptions());

            subject.MetadataValidUntil.Should().BeCloseTo(DateTime.UtcNow);
        }

        [TestMethod]
        public void Federation_ScheduledReloadOfMetadata()
        {
            MetadataRefreshScheduler.minInternval = new TimeSpan(0, 0, 0, 0, 1);

            var subject = new Federation(
                new Uri("http://localhost:13428/federationMetadataVeryShortCacheDuration"),
                false,
                StubFactory.CreateOptions());

            var initialValidUntil = subject.MetadataValidUntil;

            SpinWaiter.While(() => subject.MetadataValidUntil == initialValidUntil);
        }

        [TestMethod]
        public void Federation_ReloadOfMetadata_AddsNewIdpAndRemovesOld()
        {
            MetadataRefreshScheduler.minInternval = new TimeSpan(0, 0, 0, 0, 1);

            var options = StubFactory.CreateOptions();

            var subject = new Federation(
                new Uri("http://localhost:13428/federationMetadataVeryShortCacheDuration"),
                false,
                options);

            IdentityProvider idp;
            options.IdentityProviders.TryGetValue(new EntityId("http://idp1.federation.example.com/metadata"), out idp)
                .Should().BeTrue("idp1 should be loaded initially");
            options.IdentityProviders.TryGetValue(new EntityId("http://idp2.federation.example.com/metadata"), out idp)
                .Should().BeTrue("idp2 should be loaded initially");
            options.IdentityProviders.TryGetValue(new EntityId("http://idp3.federation.example.com/metadata"), out idp)
                .Should().BeFalse("idp3 shouldn't be loaded initially");

            MetadataServer.FederationVeryShortCacheDurationSecondAlternativeEnabled = true;
            var initialValidUntil = subject.MetadataValidUntil;
            SpinWaiter.While(() => subject.MetadataValidUntil == initialValidUntil);

            options.IdentityProviders.TryGetValue(new EntityId("http://idp1.federation.example.com/metadata"), out idp)
                .Should().BeTrue("idp1 should still be present after reload");
            options.IdentityProviders.TryGetValue(new EntityId("http://idp2.federation.example.com/metadata"), out idp)
                .Should().BeFalse("idp2 should be removed after reload");
            options.IdentityProviders.TryGetValue(new EntityId("http://idp3.federation.example.com/metadata"), out idp)
                .Should().BeTrue("idp3 should be loaded after reload");
        }
    }
}
