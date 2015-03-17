﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using NFluent;
using Ploeh.AutoFixture;
using Xunit;
using Xunit.Extensions;

namespace Takenet.SimplePersistence.Tests
{
    public abstract class MapFacts<TKey, TValue>
    {
        private readonly Fixture _fixture;

        protected MapFacts()
        {
            _fixture = new Fixture();
        } 

        public abstract IMap<TKey, TValue> Create();

        [Fact(DisplayName = "AddNewKeyAndValueSucceeds")]
        public async Task AddNewKeyAndValueSucceeds()
        {
            // Arrange
            var map = Create();
            var key = _fixture.Create<TKey>();
            var value = _fixture.Create<TValue>();

            // Act
            var result = await map.TryAddAsync(key, value, false);

            // Assert
            Check.That(result).IsTrue();
            Check.That(await map.GetValueOrDefaultAsync(key)).IsEqualTo(value);
        }

        [Fact(DisplayName = "OverwriteExistingKeyAndValueSucceeds")]
        public async Task OverwriteExistingKeyAndValueSucceeds()
        {
            // Arrange
            var map = Create();
            var key = _fixture.Create<TKey>();
            var value = _fixture.Create<TValue>();
            await map.TryAddAsync(key, value, false);
            var newValue = _fixture.Create<TValue>();

            // Act
            var result = await map.TryAddAsync(key, newValue, true);

            // Assert
            Check.That(result).IsTrue();
            Check.That(await map.GetValueOrDefaultAsync(key)).IsEqualTo(newValue);
        }

        [Fact(DisplayName = "AddExistingKeyAndValueFails")]
        public async Task AddExistingKeyAndValueFails()
        {
            // Arrange
            var map = Create();
            var key = _fixture.Create<TKey>();
            var value = _fixture.Create<TValue>();
            await map.TryAddAsync(key, value, false);
            var newValue = _fixture.Create<TValue>();

            // Act
            var result = await map.TryAddAsync(key, newValue, false);

            // Assert
            Check.That(result).IsFalse();
            Check.That(await map.GetValueOrDefaultAsync(key)).IsEqualTo(value);
        }
    }
}