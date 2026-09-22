#!/bin/bash
set -euo pipefail

project="${1:-client}"

data=""
for candidate in /opt/unity/Editor/Data /opt/Unity/Editor/Data; do
  if [ -f "${candidate}/DotNetSdkRoslyn/csc.dll" ]; then
    data="${candidate}"
    break
  fi
done
if [ -z "${data}" ]; then
  echo "::error::no Unity editor with DotNetSdkRoslyn/csc.dll in this image" >&2
  exit 1
fi

dotnet="${data}/NetCoreRuntime/dotnet"
if [ ! -x "${dotnet}" ]; then
  dotnet="$(command -v dotnet || true)"
fi
if [ -z "${dotnet}" ]; then
  echo "::error::no dotnet host to run csc.dll with" >&2
  exit 1
fi

assemblies="${project}/Library/ScriptAssemblies"
if [ ! -d "${assemblies}" ]; then
  echo "::error::${assemblies} is missing, so the editor never compiled the project" >&2
  exit 1
fi

rsp="$(mktemp)"
trap 'rm -f "${rsp}"' EXIT

{
  echo "-target:library"
  echo "-nologo"
  echo "-nostdlib+"
  echo "-define:UNITY_SERVER"
  echo "-nowarn:CS0649"
  echo "-langversion:9.0"
  echo "-out:\"$(mktemp -d)/server-check.dll\""
  find "${data}/NetStandard/ref/2.1.0" "${data}/Managed/UnityEngine" -name '*.dll' \
    | while read -r reference; do echo "-r:\"${reference}\""; done
  find "${assemblies}" -name '*.dll' \
    ! -name '*Editor*' ! -name '*Tests*' ! -name 'Blastlands.*' ! -name 'Assembly-CSharp*' \
    | while read -r reference; do echo "-r:\"${reference}\""; done
  find "${project}/Assets/_Game" -name '*.cs' ! -path '*/Tests/*' ! -path '*/Editor/*' \
    | while read -r source; do echo "\"${source}\""; done
} > "${rsp}"

echo "compiling $(grep -c '\.cs"$' "${rsp}") sources against $(grep -c '^-r:' "${rsp}") references with UNITY_SERVER"
"${dotnet}" "${data}/DotNetSdkRoslyn/csc.dll" "@${rsp}"
echo "the server build compiles"
