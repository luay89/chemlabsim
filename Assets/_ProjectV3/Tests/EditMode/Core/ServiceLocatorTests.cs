using NUnit.Framework;
using ChemLabSimV3.Core;

namespace ChemLabSimV3.Tests.EditMode.Core
{
    /// <summary>
    /// Tests for ServiceLocator — ensures service registration and resolution works.
    /// </summary>
    public class ServiceLocatorTests
    {
        public interface ITestService
        {
            string GetValue();
        }

        public class TestService : ITestService
        {
            public string GetValue() => "hello";
        }

        public class AnotherService
        {
            public int Id { get; set; }
        }

        [TearDown]
        public void Teardown()
        {
            ServiceLocator.Clear();
        }

        [Test]
        public void Register_And_Get_ByInterface()
        {
            ServiceLocator.Register<ITestService>(new TestService());
            var resolved = ServiceLocator.Get<ITestService>();

            Assert.IsNotNull(resolved);
            Assert.That(resolved.GetValue(), Is.EqualTo("hello"));
        }

        [Test]
        public void Register_And_Get_ByConcreteType()
        {
            var svc = new AnotherService { Id = 42 };
            ServiceLocator.Register(svc);

            var resolved = ServiceLocator.Get<AnotherService>();
            Assert.IsNotNull(resolved);
            Assert.That(resolved.Id, Is.EqualTo(42));
        }

        [Test]
        public void Get_UnregisteredService_ReturnsNull()
        {
            var resolved = ServiceLocator.Get<ITestService>();
            Assert.IsNull(resolved);
        }

        [Test]
        public void Has_RegisteredService_ReturnsTrue()
        {
            ServiceLocator.Register<ITestService>(new TestService());
            Assert.That(ServiceLocator.Has<ITestService>(), Is.True);
        }

        [Test]
        public void Has_UnregisteredService_ReturnsFalse()
        {
            Assert.That(ServiceLocator.Has<ITestService>(), Is.False);
        }

        [Test]
        public void Register_SameTypeTwice_Overwrites()
        {
            ServiceLocator.Register<ITestService>(new TestService());
            var first = ServiceLocator.Get<ITestService>();

            var secondService = new TestService();
            ServiceLocator.Register<ITestService>(secondService);
            var second = ServiceLocator.Get<ITestService>();

            Assert.That(second, Is.SameAs(secondService));
        }

        [Test]
        public void Clear_RemovesAllRegistrations()
        {
            ServiceLocator.Register<ITestService>(new TestService());
            ServiceLocator.Clear();

            Assert.That(ServiceLocator.Has<ITestService>(), Is.False);
        }

        [Test]
        public void RegisterNull_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => ServiceLocator.Register<ITestService>(null));
        }
    }
}
