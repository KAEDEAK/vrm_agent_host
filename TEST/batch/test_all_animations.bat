if "%VRMAH_ENDPOINT%"=="" set VRMAH_ENDPOINT=localhost:34560
@echo off
chcp 65001

REM Enable autoPrepareSeamless
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=autoPrepareSeamless&enable=true"
REM Check status
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=getAutoPrepareSeamless"

REM Idle animations
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Idle_generic&seamless=y"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Idle_angry&seamless=y"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Idle_brave&seamless=y"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Idle_calm&seamless=y"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Idle_calm_02&seamless=y"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Idle_concern&seamless=y"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Idle_classy&seamless=y"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Idle_cute&seamless=y"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Idle_denying&seamless=y"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Idle_energetic&seamless=y"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Idle_energetic_02&seamless=y"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Idle_sexy&seamless=y"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Idle_pitiable&seamless=y"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Idle_stressed&seamless=y"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Idle_surprise&seamless=y"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Idle_think&seamless=y"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Idle_what&seamless=y"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Idle_boyish&seamless=y"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Idle_cry&seamless=y"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Idle_laugh&seamless=y"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Idle_cute_02&seamless=y"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Idle_angry_02&seamless=y"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Idle_fedup&seamless=y"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Idle_fedup_02&seamless=y"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Idle_cute_03&seamless=y"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Idle_cat&seamless=y"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Idle_pointfinger&seamless=y"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Idle_energetic_03&seamless=y"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Idle_sexy_02&seamless=y"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Idle_sexy_03&seamless=y"

REM Other animations
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Other_walk&seamless=y"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Other_run&seamless=y"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Other_wave_hand&seamless=n"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Other_wave_hands&seamless=n"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Other_wave_arm&seamless=n"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Other_what&seamless=y"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Other_energetic&seamless=y"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Other_cute&seamless=y"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Other_cat&seamless=y"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Other_point_finger&seamless=y"

REM Layer animations
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Layer_start&seamless=y"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Layer_look_away&seamless=y"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Layer_look_away_angry&seamless=y"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Layer_nod_once&seamless=y"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Layer_nod_twice&seamless=y"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Layer_shake_head&seamless=y"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Layer_swing_body&seamless=y"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Layer_laugh_up&seamless=y"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Layer_laugh_down&seamless=y"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Layer_shake_body&seamless=y"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Layer_surprise&seamless=y"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Layer_tilt_neck&seamless=y"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Layer_turn_right&seamless=y"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Layer_turn_left&seamless=y"

REM BlendShape tests via shape command
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=shape&word=Aa&seamless=y"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=shape&word=Ih&seamless=y"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=shape&word=Ou&seamless=y"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=shape&word=Ee&seamless=y"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=shape&word=Oh&seamless=y"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=shape&word=reset&seamless=y"

REM Mouth alias tests
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=mouth&word=A"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=mouth&word=I"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=mouth&word=U"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=mouth&word=E"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=mouth&word=O"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=mouth&word=RESET"

REM Blink control
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=autoBlink&enabled=true&freq=1500"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=autoBlink&enabled=false"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=shape&blink=0.9,0.0"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=shape&blink=reset"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=reset_blink"

REM Additional animation controls
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=reset"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=stop"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=resume"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=getstatus"
