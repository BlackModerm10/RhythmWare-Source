using UnityEngine;
namespace RhythmWare.Core { public static class PlayerProfile { public static string DisplayName {get=>ParticipantProfiles.Current?.PublicName??"OPERADOR";set {if(ParticipantProfiles.Current!=null&&ParticipantProfiles.ValidAlias(value))ParticipantProfiles.Rename(value);}} } }
