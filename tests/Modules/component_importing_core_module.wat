(component
  (import "x:y/z" (instance $package
    (export "mod" (core module
      (export "function" (func (param i32) (result i32)))))))
  (core instance $instance (instantiate (module $package "mod")))
  (type $function (func (param "value" s32) (result s32)))
  (func $call (type $function) (canon lift (core func $instance "function")))
  (export "call-function" (func $call)))