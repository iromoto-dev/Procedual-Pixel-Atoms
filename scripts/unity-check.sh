#!/usr/bin/env bash
# Verifies the Unity package (unity/com.proceduralpixel.creations) without a Unity installation:
#   1. the package mirrors the repository (core, plantfolk, core tests, presets, .meta files)
#   2. core and plantfolk compile as Unity compiles them: Roslyn, C# 9, .NET Standard 2.1 API profile
#   3. the Unity runtime (editor and player defines), editor, tests and demo sample compile against
#      the Unity 2021.3 reference assemblies, without warnings
#   4. the core test suite, including the bit exact golden values, passes on Mono (Unity's runtime)
# Reference assemblies are downloaded once from nuget.org into .tools/unity-refs (git ignored):
#   NETStandard.Library.Ref 2.1.0, UnityEngine.Modules 2021.3.33, Unity3D.SDK 2021.1.14.1 (UnityEditor),
#   NUnit 3.5.0 (the version Unity's test framework ships).
# Needs the .NET SDK (Roslyn); Mono is optional (step 4 is skipped without it).
#   scripts/unity-check.sh [--sync]   (--sync updates the package instead of only checking it)
source "$(dirname "${BASH_SOURCE[0]}")/common.sh"
PKG="${REPO_ROOT}/unity/com.proceduralpixel.creations"
REFS="${REPO_ROOT}/.tools/unity-refs"
OUT="${REPO_ROOT}/.tools/unity-check"
mkdir -p "${REFS}" "${OUT}"
status=0

command -v dotnet >/dev/null 2>&1 || { echo "The .NET SDK is required (dotnet)." >&2; exit 1; }
# newest SDK: "8.0.133 [/usr/lib/dotnet/sdk]" -> /usr/lib/dotnet/sdk/8.0.133/Roslyn/bincore/csc.dll
SDK_LINE="$(dotnet --list-sdks | tail -1)"
CSC="$(echo "${SDK_LINE}" | sed -E 's/^([^ ]+) \[(.*)\]$/\2/')/${SDK_LINE%% *}/Roslyn/bincore/csc.dll"
[[ -f "${CSC}" ]] || { echo "Roslyn (csc.dll) not found: ${CSC}" >&2; exit 1; }

fetch() { # fetch <id> <version> <folder>
  local dir="${REFS}/$3"
  [[ -d "${dir}" ]] && return 0
  echo "downloading $1 $2"
  local tmp; tmp="$(mktemp -d)"
  local id; id="$(echo "$1" | tr '[:upper:]' '[:lower:]')"
  curl -fsSL -o "${tmp}/p.nupkg" "https://api.nuget.org/v3-flatcontainer/${id}/$2/${id}.$2.nupkg"
  mkdir -p "${dir}" && (cd "${dir}" && unzip -q -o "${tmp}/p.nupkg")
  rm -rf "${tmp}"
}
fetch NETStandard.Library.Ref 2.1.0 nsref
fetch UnityEngine.Modules 2021.3.33 modules
fetch Unity3D.SDK 2021.1.14.1 sdk
fetch NUnit 3.5.0 nunit

NSD="${REFS}/nsref/ref/netstandard2.1"
BCL=(-r:"${NSD}/netstandard.dll" -r:"${NSD}/mscorlib.dll")
UNITY=()
for f in "${REFS}"/modules/lib/netstandard2.0/UnityEngine*.dll; do UNITY+=(-r:"$f"); done
EDITOR=(-r:"$(find "${REFS}/sdk" -name UnityEditor.dll | head -1)")
NUNIT=(-r:"${REFS}/nunit/lib/net45/nunit.framework.dll")
CSFLAGS=(-nologo -noconfig -nostdlib+ -langversion:9 -warn:4 -warnaserror+ -deterministic -debug:portable)

csc() { # csc <name> <defines> <args...>
  local name="$1" defines="$2"; shift 2
  if dotnet "${CSC}" "${CSFLAGS[@]}" -define:"${defines}" "$@" > "${OUT}/${name}.log" 2>&1; then
    echo "  ok      ${name}"
  else
    echo "  FAILED  ${name}"; { grep -E "error|warning" "${OUT}/${name}.log" || cat "${OUT}/${name}.log"; } | head -40; status=1
  fi
}
sources() { find "$@" -name "*.cs" -not -path "*/Samples~/*" | sort; }

echo "== 1/4 Package sync =="
dotnet build "${REPO_ROOT}/tools/core_harness/CoreHarness.csproj" -c Release -nologo -v q > "${OUT}/harness-build.log" 2>&1 \
  || { cat "${OUT}/harness-build.log"; exit 1; }
HARNESS="${REPO_ROOT}/tools/core_harness/bin/Release/net8.0/CoreHarness.dll"
if [[ "${1:-}" == "--sync" ]]; then dotnet "${HARNESS}" unity-sync "${REPO_ROOT}" | tail -1
else dotnet "${HARNESS}" unity-sync "${REPO_ROOT}" --check | tail -1 || status=1; fi

echo "== 2/4 Core assemblies (C# 9, .NET Standard 2.1) =="
EDITOR_DEFINES="UNITY_2021_3_OR_NEWER;UNITY_EDITOR;UNITY_INCLUDE_TESTS"
PLAYER_DEFINES="UNITY_2021_3_OR_NEWER;UNITY_STANDALONE;ENABLE_IL2CPP"
csc PixelCreatures.Core "${EDITOR_DEFINES}" -target:library -out:"${OUT}/PixelCreatures.Core.dll" "${BCL[@]}" $(sources "${PKG}/Runtime/Core")
csc PixelCreatures.Plantfolk "${EDITOR_DEFINES}" -target:library -out:"${OUT}/PixelCreatures.Plantfolk.dll" "${BCL[@]}" \
  -r:"${OUT}/PixelCreatures.Core.dll" $(sources "${PKG}/Runtime/Plantfolk")

echo "== 3/4 Unity layer (Unity 2021.3 reference assemblies) =="
CORE=(-r:"${OUT}/PixelCreatures.Core.dll")
# in the editor Unity compiles runtime assemblies with UNITY_EDITOR and a reference to UnityEditor
csc PixelCreatures.Unity "${EDITOR_DEFINES}" -target:library -out:"${OUT}/PixelCreatures.Unity.dll" "${BCL[@]}" "${CORE[@]}" "${UNITY[@]}" \
  "${EDITOR[@]}" $(sources "${PKG}/Runtime/Unity")
mkdir -p "${OUT}/player"
csc PixelCreatures.Unity.Player "${PLAYER_DEFINES}" -target:library -out:"${OUT}/player/PixelCreatures.Unity.dll" "${BCL[@]}" "${CORE[@]}" "${UNITY[@]}" \
  $(sources "${PKG}/Runtime/Unity")
csc PixelCreatures.Unity.Editor "${EDITOR_DEFINES}" -target:library -out:"${OUT}/PixelCreatures.Unity.Editor.dll" "${BCL[@]}" "${CORE[@]}" \
  -r:"${OUT}/PixelCreatures.Unity.dll" "${UNITY[@]}" "${EDITOR[@]}" $(sources "${PKG}/Editor")
csc PixelCreatures.Tests.Editor "${EDITOR_DEFINES}" -target:library -out:"${OUT}/PixelCreatures.Tests.Editor.dll" "${BCL[@]}" "${CORE[@]}" \
  -r:"${OUT}/PixelCreatures.Plantfolk.dll" -r:"${OUT}/PixelCreatures.Unity.dll" -r:"${OUT}/PixelCreatures.Unity.Editor.dll" \
  "${UNITY[@]}" "${EDITOR[@]}" "${NUNIT[@]}" $(sources "${PKG}/Tests")
# the demo sample is imported into Assets/ and compiles into Assembly-CSharp (legacy Input Manager branch)
csc Sample.Demo "${EDITOR_DEFINES}" -target:library -out:"${OUT}/Sample.Demo.dll" "${BCL[@]}" "${CORE[@]}" \
  -r:"${OUT}/PixelCreatures.Unity.dll" "${UNITY[@]}" $(find "${PKG}/Samples~/Demo" -name "*.cs")

echo "== 4/4 Core tests on Mono =="
if command -v mono >/dev/null 2>&1; then
  csc CoreMono "UNITY_2021_3_OR_NEWER" -target:exe -optimize+ -out:"${OUT}/CoreMono.exe" "${BCL[@]}" \
    $(sources "${PKG}/Runtime/Core" "${PKG}/Runtime/Plantfolk" "${PKG}/Tests/Editor/Core") "${REPO_ROOT}/tools/unity_compat/MonoRunner.cs"
  mono "${OUT}/CoreMono.exe" 20 | tail -3 || status=1
else
  echo "  skipped (mono not installed)"
fi

if [[ $status -eq 0 ]]; then echo "UNITY CHECKS PASSED"; else echo "UNITY CHECKS FAILED (logs in ${OUT})"; fi
exit $status
