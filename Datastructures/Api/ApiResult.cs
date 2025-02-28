﻿namespace HlidacStatu.DS.Api
{
    public class ApiResult
    {
        public ApiResult() { }
        public ApiResult(bool success) { this.Success = success; }
        public bool Success { get; set; } = true;

        public int ErrorCode { get; set; } = 0;
        public string ErrorDescription { get; set; } = "";

        public string Server { get; set; }
    }

    public class ApiResult<T> : ApiResult
    {
        public ApiResult() { }
        public ApiResult(bool success) : base(success) { }
        public ApiResult(bool success, T data) : base(success)
        { this.Data = data; }
        public ApiResult(T data) : base(true) { this.Data = data; }


        public T Data { get; set; }
    }
}
