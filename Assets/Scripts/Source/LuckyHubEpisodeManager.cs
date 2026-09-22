using System;
using System.Collections.Generic;
using Hazel;

namespace LuckyInterview
{
    /// <summary>
    /// Episode manager for Lucky Hub - handles observations, actions, rewards, and episode boundaries
    /// </summary>
    public class LuckyHubEpisodeManager : Entity
    {
        [Group("Scene References")]
        public Entity? taskController = null; // Reference to Take_ServerPlugger
        
        [Group("Scene References")]
        public Entity? armLeft = null;
        
        [Group("Scene References")]
        public Entity? armRight = null;
        
        [Group("Scene References")]
        public Entity? cable = null;
        
        [Group("Scene References")]
        public Entity? targetPort = null;
        
        [Group("Observation Cameras")]
        public Entity? overviewCamera = null;
        
        [Group("Observation Cameras")]
        public Entity? taskCamera = null;
        
        [Group("Episode Settings")]
        public float maxEpisodeTime = 120.0f;
        
        [Group("Episode Settings")]
        public float successDistance = 0.05f; // 5cm tolerance for successful insertion
        
        [Group("Episode Settings")]
        public bool autoReset = true;
        
        [Group("Reward Settings")]
        public float successReward = 100.0f;
        
        [Group("Reward Settings")]
        public float timeoutPenalty = -10.0f;
        
        [Group("Reward Settings")]
        public float progressRewardScale = 1.0f;

        // Episode state
        private bool _episodeActive = false;
        private float _episodeElapsedTime = 0f;
        private float _cumulativeReward = 0f;
        private int _episodeCount = 0;
        
        // Initial positions for reset
        private Transform _initialCableTransform;
        private Dictionary<Entity, Transform> _initialTransforms = new();
        
        // Task progress tracking
        private Vector3 _cableStartPosition;
        private Vector3 _targetPosition;
        private float _bestDistance = float.MaxValue;

        protected override void OnCreate()
        {
            Log.Info("LuckyHub Episode Manager initialized");
            
            if (!ValidateReferences())
            {
                Log.Error("Missing required references in LuckyHubEpisodeManager");
                return;
            }
            
            StoreInitialTransforms();
        }

        /// <summary>
        /// Initialize a new episode
        /// </summary>
        [Button("Start Episode")]
        public void InitializeEpisode()
        {
            Log.Info($"Starting episode {_episodeCount + 1}");
            
            _episodeActive = true;
            _episodeElapsedTime = 0f;
            _cumulativeReward = 0f;
            _bestDistance = float.MaxValue;
            _episodeCount++;
            
            // Reset scene to initial state
            ResetScene();
            
            // Initialize task tracking
            if (cable != null && targetPort != null)
            {
                _cableStartPosition = cable.Transform.Translation;
                _targetPosition = targetPort.Transform.Translation;
            }
            
            // Start the task
            var controller = taskController?.As<Take_ServerPlugger>();
            if (controller != null)
            {
                Log.Info("Starting task controller episode");
                // The Take_ServerPlugger will handle its own episode logic
            }
        }

        protected override void OnUpdate(float deltaTime)
        {
            if (!_episodeActive) return;
            
            _episodeElapsedTime += deltaTime;
            
            CheckEpisodeStatus();
        }

        /// <summary>
        /// Check episode status and handle termination
        /// </summary>
        [Button("Check Episode Status")]
        public void CheckEpisodeStatus()
        {
            if (!_episodeActive) return;
            
            // Check for episode termination conditions
            if (_episodeElapsedTime > maxEpisodeTime)
            {
                EndEpisode(EpisodeResult.Timeout);
                return;
            }
            
            if (CheckTaskSuccess())
            {
                EndEpisode(EpisodeResult.Success);
                return;
            }
            
            // Calculate current progress
            CalculateProgress();
        }

        /// <summary>
        /// End the current episode
        /// </summary>
        public void EndEpisode(EpisodeResult result)
        {
            _episodeActive = false;
            float episodeDuration = _episodeElapsedTime;
            
            // Apply final reward
            switch (result)
            {
                case EpisodeResult.Success:
                    _cumulativeReward += successReward;
                    Log.Info($"Episode {_episodeCount} SUCCESS! Duration: {episodeDuration:F2}s, Total Reward: {_cumulativeReward:F2}");
                    break;
                    
                case EpisodeResult.Timeout:
                    _cumulativeReward += timeoutPenalty;
                    Log.Warn($"Episode {_episodeCount} TIMEOUT after {episodeDuration:F2}s, Total Reward: {_cumulativeReward:F2}");
                    break;
                    
                case EpisodeResult.Failure:
                    Log.Warn($"Episode {_episodeCount} FAILED after {episodeDuration:F2}s, Total Reward: {_cumulativeReward:F2}");
                    break;
            }
            
            // Log episode statistics for Lucky Hub
            LogEpisodeData(result, episodeDuration);
            
            if (autoReset)
            {
                // Schedule next episode
                Log.Info("Auto-reset enabled, will start new episode in 2 seconds");
            }
        }

        /// <summary>
        /// Reset scene to initial state
        /// </summary>
        [Button("Reset Scene")]
        public void ResetScene()
        {
            Log.Info("Resetting scene to initial state");
            
            // Reset cable position
            if (cable != null)
            {
                cable.Transform.Translation = _initialCableTransform.Position;
                cable.Transform.Rotation = _initialCableTransform.Rotation;
            }
            
            // Reset other entity positions
            foreach (var kvp in _initialTransforms)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.Transform.Translation = kvp.Value.Position;
                    kvp.Key.Transform.Rotation = kvp.Value.Rotation;
                }
            }
        }

        /// <summary>
        /// Get current observations for the AI model (Lucky Hub interface)
        /// </summary>
        [Button("Log Current Observations")]
        public void LogCurrentObservations()
        {
            var obs = GetObservations();
            Log.Info($"OBSERVATIONS: Cable at {obs.CablePosition}, Target at {obs.TargetPosition}, Distance: {obs.DistanceToTarget:F4}m");
        }

        public ObservationData GetObservations()
        {
            var obs = new ObservationData();
            
            // Object positions
            if (cable != null)
            {
                obs.CablePosition = cable.Transform.Translation;
                obs.CableRotation = cable.Transform.Rotation;
            }
            
            if (targetPort != null)
            {
                obs.TargetPosition = targetPort.Transform.Translation;
                obs.TargetRotation = targetPort.Transform.Rotation;
            }
            
            // Task progress
            obs.DistanceToTarget = cable != null && targetPort != null ? 
                Vector3.Distance(cable.Transform.Translation, targetPort.Transform.Translation) : 0f;
            
            obs.EpisodeTime = _episodeActive ? _episodeElapsedTime : 0f;
            obs.IsEpisodeActive = _episodeActive;
            
            return obs;
        }

        private bool ValidateReferences()
        {
            bool valid = true;
            
            if (taskController == null) { Log.Error("Task controller reference missing"); valid = false; }
            if (cable == null) { Log.Error("Cable reference missing"); valid = false; }
            if (targetPort == null) { Log.Error("Target port reference missing"); valid = false; }
            
            return valid;
        }

        private void StoreInitialTransforms()
        {
            if (cable != null)
            {
                _initialCableTransform = new Transform
                {
                    Position = cable.Transform.Translation,
                    Rotation = cable.Transform.Rotation,
                    Scale = cable.Transform.Scale
                };
            }
            
            _initialTransforms.Clear();
            if (armLeft != null) 
            {
                _initialTransforms[armLeft] = new Transform
                {
                    Position = armLeft.Transform.Translation,
                    Rotation = armLeft.Transform.Rotation,
                    Scale = armLeft.Transform.Scale
                };
            }
            if (armRight != null) 
            {
                _initialTransforms[armRight] = new Transform
                {
                    Position = armRight.Transform.Translation,
                    Rotation = armRight.Transform.Rotation,
                    Scale = armRight.Transform.Scale
                };
            }
            
            Log.Info("Initial transforms stored for episode reset");
        }

        private bool CheckTaskSuccess()
        {
            if (cable == null || targetPort == null) return false;
            
            float distance = Vector3.Distance(
                cable.Transform.Translation, 
                targetPort.Transform.Translation
            );
            
            return distance < successDistance;
        }

        private void CalculateProgress()
        {
            if (cable == null || targetPort == null) return;
            
            float currentDistance = Vector3.Distance(
                cable.Transform.Translation, 
                targetPort.Transform.Translation
            );
            
            // Track best distance achieved
            if (currentDistance < _bestDistance)
            {
                float progressReward = (_bestDistance - currentDistance) * progressRewardScale;
                _cumulativeReward += progressReward;
                _bestDistance = currentDistance;
                Log.Info($"Progress! Distance to target: {currentDistance:F4}m, Reward: +{progressReward:F2}");
            }
        }

        private void LogEpisodeData(EpisodeResult result, float duration)
        {
            // This data structure can be logged to files for Lucky Hub training
            var finalDistance = cable != null && targetPort != null ? 
                Vector3.Distance(cable.Transform.Translation, targetPort.Transform.Translation) : 0f;
            
            // Log structured data for Lucky Hub capture
            string episodeJson = $"{{\"episode\":{_episodeCount},\"result\":\"{result}\",\"duration\":{duration:F2},\"reward\":{_cumulativeReward:F2},\"distance\":{finalDistance:F4},\"timestamp\":\"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\"}}";
            
            Log.Info($"EPISODE_DATA: {episodeJson}");
        }

        public enum EpisodeResult
        {
            Success,
            Failure,
            Timeout
        }
    }

    /// <summary>
    /// Observation data structure for AI model
    /// </summary>
    public struct ObservationData
    {
        public Vector3 CablePosition;
        public Vector3 CableRotation;
        public Vector3 TargetPosition;
        public Vector3 TargetRotation;
        public float DistanceToTarget;
        public float EpisodeTime;
        public bool IsEpisodeActive;
    }
}