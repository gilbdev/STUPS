﻿/*
 * Created by SharpDevelop.
 * User: Alexander Petrovskiy
 * Date: 12/14/2013
 * Time: 1:21 AM
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */

namespace UIAutomation
{
    using System;
    using Castle.DynamicProxy;
    
    /// <summary>
    /// Description of ErrorHandlingAspect.
    /// </summary>
    public class ErrorHandlingAspect : AbstractInterceptor
    {
        public override void Intercept(IInvocation invocation)
        {
            invocation.Proceed();
//            try {
//                invocation.Proceed();
//            } catch (Exception eOnInvocation) {
//                Exception eNewException =
//                    new Exception(eOnInvocation.Message);
//                throw eNewException;
//            }
            
        }
    }
}
