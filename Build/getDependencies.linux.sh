#!/bin/bash
# server=build.palaso.org
# project=Phonology Assistant
# build=pa-develop-linux64-continuous
# root_dir=..
# Auto-generated by https://github.com/chrisvire/BuildUpdate.
# Do not edit this file by hand!

cd "$(dirname "$0")"

# *** Functions ***
force=0
clean=0

while getopts fc opt; do
case $opt in
f) force=1 ;;
c) clean=1 ;;
esac
done

shift $((OPTIND - 1))

copy_auto() {
if [ "$clean" == "1" ]
then
echo cleaning $2
rm -f ""$2""
else
where_curl=$(type -P curl)
where_wget=$(type -P wget)
if [ "$where_curl" != "" ]
then
copy_curl $1 $2
elif [ "$where_wget" != "" ]
then
copy_wget $1 $2
else
echo "Missing curl or wget"
exit 1
fi
fi
}

copy_curl() {
echo "curl: $2 <= $1"
if [ -e "$2" ] && [ "$force" != "1" ]
then
curl -# -L -z $2 -o $2 $1
else
curl -# -L -o $2 $1
fi
}

copy_wget() {
echo "wget: $2 <= $1"
f1=$(basename $1)
f2=$(basename $2)
cd $(dirname $2)
wget -q -L -N $1
# wget has no true equivalent of curl's -o option.
# Different versions of wget handle (or not) % escaping differently.
# A URL query is the only reason why $f1 and $f2 should differ.
if [ "$f1" != "$f2" ]; then mv $f2\?* $f2; fi
cd -
}


# *** Results ***
# build: pa-develop-linux64-continuous (PhonologyAssistant_PaDevelopLinux64continuous)
# project: Phonology Assistant
# URL: http://build.palaso.org/viewType.html?buildTypeId=PhonologyAssistant_PaDevelopLinux64continuous
# VCS: https://github.com/sillsdev/phonology-assistant.git [develop]
# dependencies:
# [0] build: palaso-trusty64-master Continuous (bt324)
#     project: libpalaso
#     URL: http://build.palaso.org/viewType.html?buildTypeId=bt324
#     clean: false
#     revision: phonologyassistant.tcbuildtag
#     paths: {"L10NSharp.dll"=>"Lib", "L10NSharp.pdb"=>"Lib", "SIL.Core.dll"=>"Lib", "SIL.Core.dll.config"=>"Lib", "SIL.Core.pdb"=>"Lib", "SIL.Windows.Forms.dll"=>"Lib", "SIL.Windows.Forms.dll.config"=>"Lib", "SIL.Windows.Forms.pdb"=>"Lib"}
#     VCS: https://github.com/sillsdev/libpalaso.git [master]

# make sure output directories exist
mkdir -p ../Lib

# download artifact dependencies
copy_auto http://build.palaso.org/guestAuth/repository/download/bt324/phonologyassistant.tcbuildtag/L10NSharp.dll ../Lib/L10NSharp.dll
copy_auto http://build.palaso.org/guestAuth/repository/download/bt324/phonologyassistant.tcbuildtag/L10NSharp.pdb ../Lib/L10NSharp.pdb
copy_auto http://build.palaso.org/guestAuth/repository/download/bt324/phonologyassistant.tcbuildtag/SIL.Core.dll ../Lib/SIL.Core.dll
copy_auto http://build.palaso.org/guestAuth/repository/download/bt324/phonologyassistant.tcbuildtag/SIL.Core.dll.config ../Lib/SIL.Core.dll.config
copy_auto http://build.palaso.org/guestAuth/repository/download/bt324/phonologyassistant.tcbuildtag/SIL.Core.pdb ../Lib/SIL.Core.pdb
copy_auto http://build.palaso.org/guestAuth/repository/download/bt324/phonologyassistant.tcbuildtag/SIL.Windows.Forms.dll ../Lib/SIL.Windows.Forms.dll
copy_auto http://build.palaso.org/guestAuth/repository/download/bt324/phonologyassistant.tcbuildtag/SIL.Windows.Forms.dll.config ../Lib/SIL.Windows.Forms.dll.config
copy_auto http://build.palaso.org/guestAuth/repository/download/bt324/phonologyassistant.tcbuildtag/SIL.Windows.Forms.pdb ../Lib/SIL.Windows.Forms.pdb
# End of script
