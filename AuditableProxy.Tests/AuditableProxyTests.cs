using System.Linq;
using AuditableObject;
using FluentAssertions;
using NUnit.Framework;

namespace CastleDynamicProxy
{
	[TestFixture]
	public class AuditablePetBase
	{
		protected Pet _auditablePet;

		private static class Given
		{
			public static Pet AnAuditablePet
			{
				get { return AuditableProxy.MakeAuditable<Pet>(); }
			}
		}

		[SetUp]
		public void Setup()
		{
			_auditablePet = Given.AnAuditablePet;
		}
	}

	[TestFixture]
	public class WhenWorkingWithAnAuditablePet : AuditablePetBase
	{
		[Test]
		public void It_should_work_normally()
		{
			_auditablePet.Age = 3;
			_auditablePet.Deceased = true;
			_auditablePet.Name = "Rex";
			_auditablePet.Age += _auditablePet.Name.Length;
			_auditablePet.ToString();
		}

		[Test]
		public void IsBeingAudited_should_be_false_for_before_they_are_TrackingChanges()
		{
			AuditableProxy.IsBeingAudited(_auditablePet).Should().BeFalse();
		}

		[Test]
		public void IsBeingAudited_should_be_true_after_they_are_TrackingChanges()
		{
			_auditablePet.StartTrackingChanges();
			AuditableProxy.IsBeingAudited(_auditablePet).Should().BeTrue();
		}

		[Test]
		public void IsAuditedable_should_be_true_for_objects_created_with_MakeFreezable()
		{
			_auditablePet.IsAuditable().Should().BeTrue();
		}

		[Test]
		public void Audited_object_should_track_last_change()
		{
			_auditablePet.Age = 1;
			_auditablePet.StartTrackingChanges();
			_auditablePet.Age = 2;

			var delta = _auditablePet.GetChanges(x => x.Age);
			delta.OldValue.Should().Be(1);
			delta.NewValue.Should().Be(2);
		}

		[Test]
		public void Audited_object_correctly_tracks_multiple_changes_and_new_value_should_be_last_change()
		{
			_auditablePet.Age = 1;
			_auditablePet.StartTrackingChanges();
			_auditablePet.Age = 2;
			_auditablePet.Age = 3;

			var delta = _auditablePet.GetChanges(x => x.Age);
			delta.OldValue.Should().Be(1);
			delta.NewValue.Should().Be(3);
		}

		[Test]
		public void Audited_object_has_defaults_if_you_start_tracking_changes_immediately()
		{
			_auditablePet.StartTrackingChanges();
			_auditablePet.Age = 1;
			_auditablePet.Age = 2;
			_auditablePet.Age = 3;

			var delta = _auditablePet.GetChanges(x => x.Age);
			delta.OldValue.Should().Be(default(int));
			delta.NewValue.Should().Be(3);
		}

		[Test]
		public void Audited_object_should_return_only_1_delta_when_only_1_property_has_changed()
		{
			_auditablePet.StartTrackingChanges();
			_auditablePet.Age = 1;
			_auditablePet.Age = 2;
			_auditablePet.Age = 3;

			var changes = _auditablePet.GetChanges().ToList();
			changes.Count().Should().Be(1);
			changes.Count(x => x.Key == "Age").Should().Be(1);

			var delta = changes.Single(x => x.Key == "Age").Value;
			delta.OldValue.Should().Be(default(int));
			delta.NewValue.Should().Be(3);
		}

		[Test]
		public void Audited_object_will_return_only_deltas_for_modified_objects()
		{
			_auditablePet.Age = 1;
			_auditablePet.Name = "Initial Value";
			_auditablePet.StartTrackingChanges();
			_auditablePet.Age = 2;
			_auditablePet.Name = "Changed Value";

			var results = _auditablePet.GetChanges();
			results.Count().Should().Be(2);

			var ageDelta = results["Age"];
			ageDelta.OldValue.Should().Be(1);
			ageDelta.NewValue.Should().Be(2);

			var nameDelta = results["Name"];
			nameDelta.OldValue.Should().Be("Initial Value");
			nameDelta.NewValue.Should().Be("Changed Value");
		}

		[Test]
		public void Stop_tracking_changes_should_not_record_any_new_changes()
		{
			_auditablePet.Age = 1;
			_auditablePet.StartTrackingChanges();
			_auditablePet.Age = 2;
			_auditablePet.StopTrackingChanges();
			_auditablePet.Age = 3;

			var delta = _auditablePet.GetChanges(x => x.Age);
			delta.OldValue.Should().Be(1);
			delta.NewValue.Should().Be(2);
		}

		[Test]
		public void ExcludeProperties_should_not_track_changes()
		{
			_auditablePet.ExcludeProperty(x => x.Age);
			_auditablePet.StartTrackingChanges();
			_auditablePet.Age = 1;
			_auditablePet.Age = 2;
			_auditablePet.Age = 3;

			var delta = _auditablePet.GetChanges(x => x.Age);
			delta.Should().BeNull();
		}

		[Test]
		public void GetChanges_will_return_stronglyTyped_results()
		{
			// strongly typed results
			_auditablePet.Age = 1;
			_auditablePet.StartTrackingChanges();
			_auditablePet.Age = 2;

			IDelta<int> delta = _auditablePet.GetChanges(x => x.Age);
			delta.Should().NotBeNull();
			delta.OldValue.Should().Be(1);
			delta.NewValue.Should().Be(2);
		}

		[Test]
		public void GetChanges_will_return_stronglyTyped_results2()
		{
			// strongly typed results
			_auditablePet.Age = 1;
			_auditablePet.StartTrackingChanges();
			_auditablePet.Age = 2;

			IDelta<int> delta = _auditablePet.GetChanges(x => x.Age);
			delta.Should().NotBeNull();
			delta.OldValue.Should().Be(1);
			delta.NewValue.Should().Be(2);
		}

		// test for virtual properties
	}

	[TestFixture]
	public class NonAuditablePetBase
	{
		protected Pet _nonAuditablePet;

		[SetUp]
		public void Setup()
		{
			_nonAuditablePet = new Pet();
		}
	}

	[TestFixture]
	public class WhenWorkingWithANonAuditedObject : NonAuditablePetBase
	{
		[Test, ExpectedException(typeof(NotBeingAuditedException))]
		public void Calling_start_should_throw_an_exception()
		{
			_nonAuditablePet.StartTrackingChanges();
		}

		[Test, ExpectedException(typeof(NotBeingAuditedException))]
		public void Calling_stop_should_throw_an_exception()
		{
			_nonAuditablePet.StopTrackingChanges();
		}

		[Test, ExpectedException(typeof(NotBeingAuditedException))]
		public void Calling_getChanges_should_throw_an_exception()
		{
			_nonAuditablePet.GetChanges(x => x.Name);
		}

		[Test, ExpectedException(typeof(NotBeingAuditedException))]
		public void Calling_excludeProperty_should_throw_an_exception()
		{
			_nonAuditablePet.ExcludeProperty(x => x.Name);
		}

		[Test]
		public void IsAuditable_should_be_false_for_objects_created_with_ctor()
		{
			_nonAuditablePet.IsAuditable().Should().BeFalse();
		}

		[Test]
		public void IsBeingAudited_should_be_false_for_not_auditable_objects()
		{
			AuditableProxy.IsBeingAudited(_nonAuditablePet).Should().BeFalse();
		}
	}
}
