// ChemLabSim v3 — IService interface
// All v3 services implement this for uniform lifecycle management by V3Bootstrap.

namespace ChemLabSimV3.Services
{
    public interface IService
    {
        /// <summary>Called once by V3Bootstrap during initialization.</summary>
        void Initialize();

        /// <summary>Called on application quit or teardown.</summary>
        void Dispose();
    }
}
