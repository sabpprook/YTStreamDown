@echo off
chcp 65001

if "%~n1" == "" goto :eof

(
echo #EXTM3U
echo #EXT-X-VERSION:6
echo #EXT-X-TARGETDURATION:1
echo #EXT-X-MEDIA-SEQUENCE:0
) > video.m3u8

copy video.m3u8 audio.m3u8

for %%1 in (%~n1\video\*.*) do echo #EXTINF:1.0,>>video.m3u8 & echo %%1>>video.m3u8
for %%1 in (%~n1\audio\*.*) do echo #EXTINF:1.0,>>audio.m3u8 & echo %%1>>audio.m3u8
echo #EXT-X-ENDLIST>>video.m3u8
echo #EXT-X-ENDLIST>>audio.m3u8

set /p title=<title.txt

ffmpeg -y -i video.m3u8 -i audio.m3u8 -i cover.jpg -c copy -map 0:v:0 -map 1:a:0 -map 2 -disposition:2 attached_pic -metadata title="%title%" "%~n1\@output.mp4"

del video.m3u8 audio.m3u8

exit 0
