(component
  (import "wasm-callback" (func $wasm-callback (param "value" s32) (result s32)))
  (core func $callback-lowered (canon lower (func $wasm-callback)))
  (core module $m
    (import "" "wasm-callback" (func $wasm-callback (param i32) (result i32)))
    (func (export "call-wasm-callback") (param i32) (result i32)
      local.get 0
      call $wasm-callback))
  (core instance $imports
    (export "wasm-callback" (func $callback-lowered)))
  (core instance $i (instantiate $m (with "" (instance $imports))))
  (type $t (func (param "value" s32) (result s32)))
  (func $call (type $t) (canon lift (core func $i "call-wasm-callback")))
  (export "call-wasm-callback" (func $call)))